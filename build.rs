use std::env;
use std::fs;
use std::io::{Error, ErrorKind, Result};
use std::path::{Path, PathBuf};
use std::process::Command;

/// Check that dotnet is installed
fn check_tools() -> Result<()> {
    let output = Command::new("dotnet").arg("--version").output()?;
    if !output.status.success() {
        return Err(Error::new(
            ErrorKind::NotFound,
            "dotnet is not installed or not in PATH",
        ));
    }
    Ok(())
}

/// Check that ext/Mirage.dll and ext/Assembly-CSharp.dll exist, if not try to find them on user
/// disk and copy them here.
fn populate_ext_dlls() -> Result<()> {
    const DEFAULT_NUCLEAR_OPTION_PATH: &str =
        r"C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option\NuclearOption_Data\Managed";
    const NUCLEAR_OPTION_PATH: &str = "NUCLEAR_OPTION_PATH";

    let dlls = ["Mirage.dll", "Assembly-CSharp.dll"];
    let ext_dir = Path::new("ext");
    fs::create_dir_all(ext_dir)?;

    for dll in &dlls {
        let dest = ext_dir.join(dll);
        if dest.exists() {
            continue;
        }

        // Try default path first, then env variable
        let default_src = Path::new(DEFAULT_NUCLEAR_OPTION_PATH).join(dll);
        if default_src.exists() {
            fs::copy(&default_src, &dest)?;
            continue;
        }

        if let Ok(custom_path) = env::var(NUCLEAR_OPTION_PATH) {
            let custom_src = Path::new(&custom_path).join(dll);
            if custom_src.exists() {
                fs::copy(&custom_src, &dest)?;
                continue;
            }
        }

        return Err(Error::new(
            ErrorKind::NotFound,
            format!(
                "Could not find {dll}. Place it in ext/ or set {NUCLEAR_OPTION_PATH} to the \
                 NuclearOption_Data\\Managed directory."
            ),
        ));
    }

    Ok(())
}

/// Make cargo watch csharp sources
fn add_dotnet_sources() -> Result<()> {
    println!("cargo:rerun-if-changed=src/Directory.Build.props");
    println!("cargo:rerun-if-changed=zorduzd.sln");

    for entry in fs::read_dir("src")? {
        let path = entry?.path();
        if let Some(ext) = path.extension() {
            let ext = ext.to_string_lossy();
            if ext == "cs" || ext == "csproj" {
                println!("cargo:rerun-if-changed={}", path.display());
            }
        }
    }

    Ok(())
}

/// Call dotnet build
fn build_dot_net() -> Result<()> {
    let profile = env::var("PROFILE").unwrap_or_else(|_| "debug".to_string());
    let configuration = if profile == "release" {
        "Release"
    } else {
        "Debug"
    };

    let output = Command::new("dotnet")
        .args(["build", "--configuration", configuration])
        .output()?;

    let stderr = String::from_utf8_lossy(&output.stderr);
    let stdout = String::from_utf8_lossy(&output.stdout);
    println!("{stdout}");
    eprintln!("{stderr}");
    if !output.status.success() {
        return Err(Error::other("dotnet build failed"));
    }

    Ok(())
}

/// Copy dotnet outputs to where cargo puts its outputs
fn copy_dot_net_objects() -> Result<()> {
    let profile = env::var("PROFILE").unwrap_or_else(|_| "debug".to_string());
    let configuration = if profile == "release" {
        "Release"
    } else {
        "Debug"
    };

    let dotnet_out: PathBuf = ["target", "bepinex", "bin", configuration, "netstandard2.1"]
        .iter()
        .collect();

    // OUT_DIR is target/{profile}/build/{crate}-{hash}/out — go up 3 to get target/{profile}/
    let out_dir = PathBuf::from(env::var("OUT_DIR").map_err(Error::other)?);
    let cargo_out = out_dir
        .parent()
        .and_then(|p| p.parent())
        .and_then(|p| p.parent())
        .ok_or_else(|| Error::other("could not determine cargo binary output directory"))?;

    let files = ["zorduzd.dll", "zorduzd.pdb", "zorduzd.deps.json"];
    for file in &files {
        let src = dotnet_out.join(file);
        let dest = cargo_out.join(file);
        fs::copy(&src, &dest).map_err(|e| Error::other(format!("{}: {e}", src.display())))?;
    }

    Ok(())
}

fn main() -> Result<()> {
    check_tools()?;
    populate_ext_dlls()?;
    add_dotnet_sources()?;
    build_dot_net()?;
    copy_dot_net_objects()
}
