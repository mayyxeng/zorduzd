#![windows_subsystem = "windows"]
use std::sync::Arc;

use zorduzd::app::App;

use eframe::NativeOptions;
use egui::ViewportBuilder;
use tokio::runtime::Builder;
use zorduzd::worker::*;
fn main() {
    env_logger::init();
    let runtime = Builder::new_multi_thread()
        .worker_threads(2)
        .enable_all()
        .build()
        .expect("failed to build runtime");
    let shared_state: Arc<SharedState> = Default::default();
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
