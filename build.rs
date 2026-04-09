use std::io::Result;
use std::path::PathBuf;
/// check that ext/Mirage.dll and ext/Assembly-CSharp.dll exists, if not try to find them on user
/// disk and copy them here.
fn populate_ext_dlls() -> Result<()> {
    // default path
    const DEFAULT_NUCLEAR_OPTION_PATH: &str = r#"C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option\NuclearOption_Data\Managed\"#;
    // env variable name to look up if the default path did not contain the required dlls
    const NUCLEAR_OPTION_PATH: &str = "NUCLEAR_OPTION_PATH";
    // look for :
    // NuclearOption_Data\Managed\Mirage.dll, and
    // NuclearOption_Data\Managed\Assembly-CSharp.dll under the default path and copy them to ext directory.
    // if not found under default path, try looking up the env variable, if the variable does not
    // exists or files do not exist under the given path report an appropriate error.
    todo!("fill in!")
}

fn add_dotnet_sources() -> Result<()> {
    // add extra cargo watch dependencies (any *.cs, *.sln, *.Build.props)
    todo!("fill in!")
}
fn build_dot_net() -> Result<()> {
    // build the dotnet project, ensure to pass appropriate debug or release arguments based on the
    // rust build
    todo!("fill in!")
}
fn copy_dot_net_objects() -> Result<()> {
    // copy the dotnet outputs:
    // zordozd.dll
    // zordozd.pbd
    // zordozd.deps.json
    // into the rust build output directory
    todo!("fill in!");
}

fn main() -> Result<()> {
    populate_ext_dlls()?;
    add_dotnet_sources()?;
    build_dot_net()?;
    copy_dot_net_objects()
}
