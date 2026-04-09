#![windows_subsystem = "windows"]
use std::sync::Arc;

use zorduzd::app::App;

use eframe::NativeOptions;
use egui::ViewportBuilder;
use tokio::runtime::Builder;
use zorduzd::worker::*;

#[derive(serde::Deserialize, serde::Serialize)]
struct Cfg {
    ports: CfgPorts,
}

#[derive(serde::Deserialize, serde::Serialize)]
struct CfgPorts {
    moza: u16,
    game: u16,
}

impl Cfg {
    fn from_shared_state(state: &SharedState) -> Self {
        Self {
            ports: CfgPorts {
                moza: state.moza_port.read(),
                game: state.game_port.read(),
            },
        }
    }
}

fn load_cfg(shared_state: &SharedState) {
    let exe_dir = std::env::current_exe()
        .ok()
        .and_then(|p| p.parent().map(|d| d.to_path_buf()));
    let Some(dir) = exe_dir else { return };
    let cfg_path = dir.join("zorduzd.cfg");
    if !cfg_path.exists() {
        let defaults = Cfg::from_shared_state(shared_state);
        match toml::to_string_pretty(&defaults) {
            Ok(contents) => {
                if let Err(e) = std::fs::write(&cfg_path, contents) {
                    log::warn!("could not write default {}: {e}", cfg_path.display());
                }
            }
            Err(e) => log::warn!("could not serialize default config: {e}"),
        }
        return;
    }
    let contents = match std::fs::read_to_string(&cfg_path) {
        Ok(c) => c,
        Err(e) => {
            log::warn!("could not read {}: {e}", cfg_path.display());
            return;
        }
    };
    let cfg: Cfg = match toml::from_str(&contents) {
        Ok(c) => c,
        Err(e) => {
            log::error!("failed to parse {}: {e}", cfg_path.display());
            return;
        }
    };
    shared_state.moza_port.write(cfg.ports.moza);
    shared_state.game_port.write(cfg.ports.game);
}

fn main() {
    let shared_state: Arc<SharedState> = Default::default();
    env_logger::init();
    load_cfg(&shared_state);
    let runtime = Builder::new_multi_thread()
        .worker_threads(2)
        .enable_all()
        .build()
        .expect("failed to build runtime");
    let mut worker = Worker::new(Arc::clone(&shared_state));
    runtime.spawn(async move {
        worker.run().await;
    });
    let app = App::new(Arc::clone(&shared_state));
    let native_options = NativeOptions {
        viewport: ViewportBuilder::default().with_min_inner_size([750.0, 850.0]),
        ..Default::default()
    };
    log::info!("starting the ui");
    eframe::run_native(
        "Moza FFB DCS Proxy",
        native_options,
        Box::new(|_cc| Ok(Box::new(app))),
    )
    .unwrap();
    shared_state.ui_online.set_offline();
}
