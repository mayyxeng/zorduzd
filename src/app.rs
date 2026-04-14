use crate::UiTunnable;
use crate::worker::SharedState;
use std::path::PathBuf;
use std::sync::Arc;

pub struct App {
    shared_state: Arc<SharedState>,
    reactive: bool,
    show_dcs_setup_popup: bool,
    dcs_setup_result: Option<Result<(), String>>,
}

fn dcs_saved_games_path() -> PathBuf {
    let user_profile =
        std::env::var("USERPROFILE").unwrap_or_else(|_| r"C:\Users\Unknown".to_string());
    PathBuf::from(user_profile).join("Saved Games").join("DCS")
}

impl App {
    pub fn new(shared_state: Arc<SharedState>) -> Self {
        Self {
            shared_state,
            reactive: true,
            show_dcs_setup_popup: false,
            dcs_setup_result: None,
        }
    }
    fn draw_ui(&mut self, ui: &mut egui::Ui) {
        egui::Panel::top("top").show_inside(ui, |ui| {
            ui.horizontal(|ui| {
                egui::widgets::global_theme_preference_buttons(ui);
                if ui
                    .selectable_label(!self.reactive, "▶ Continuous")
                    .on_hover_text("Continuously update the screen")
                    .clicked()
                {
                    self.reactive = !self.reactive;
                };
            });
        });
        egui::Panel::bottom("bottom")
            // .exact_height(300.0)
            .show_inside(ui, |ui| {
                ui.vertical(|ui| {
                    // ui.set_min_width(350.0);
                    ui.horizontal(|ui| {
                        ui.label("Logs");
                        if ui.button("Clear Logs").clicked() {
                            self.shared_state.logs.write().clear();
                        }
                    });
                    ui.separator();
                    egui::ScrollArea::vertical()
                        .stick_to_bottom(true)
                        .show(ui, |ui| {
                            for log in self.shared_state.logs.read().iter() {
                                ui.label(log);
                            }
                        });
                });
            });

        egui::CentralPanel::default().show_inside(ui, |ui| {
            ui.heading(egui::RichText::new("Moza FFB DCS Proxy").strong());
            ui.vertical(|ui| {
                ui.horizontal(|ui| {
                    let server_online = self.shared_state.server_online.online();
                    let moza_online = self.shared_state.moza_online.online();
                    let game_online = self.shared_state.game_online.online();
                    let moza_port = Some(&self.shared_state.moza_port);
                    let game_port = Some(&self.shared_state.game_port);
                    for (name, port, state, tooltip) in [
                        (
                            "server",
                            None,
                            server_online,
                            "TCP server for Moza Cockpit connections",
                        ),
                        (
                            "moza cockpit",
                            moza_port,
                            moza_online,
                            "Moza Cockpit application connection",
                        ),
                        (
                            "nuclear option",
                            game_port,
                            game_online,
                            "Nuclear Option game telemetry connection",
                        ),
                    ] {
                        ui.add(egui::Label::new(name));
                        if state {
                            ui.add(egui::Label::new(egui::RichText::new("online").strong()))
                                .on_hover_text(tooltip);
                        } else {
                            ui.add(egui::Label::new(egui::RichText::new("offline").weak()))
                                .on_hover_text(tooltip);
                        }
                        if let Some(tcp_port) = port {
                            let value = tcp_port.read();
                            ui.add(egui::Label::new(
                                egui::RichText::new(value.to_string())
                                    .monospace()
                                    .small_raised(),
                            ));
                        }
                        ui.separator();
                    }
                    let dcs_path = dcs_saved_games_path();
                    let dcs_online = dcs_path.is_dir();
                    ui.add(egui::Label::new("DCS"));
                    if dcs_online {
                        ui.add(egui::Label::new(egui::RichText::new("online").strong()))
                            .on_hover_text("DCS Saved Games directory found");
                    } else {
                        ui.add(egui::Label::new(egui::RichText::new("offline").weak()))
                            .on_hover_text("DCS Saved Games directory not found");
                    }
                    if ui
                        .add_enabled(!dcs_online, egui::Button::new("setup DCS"))
                        .clicked()
                    {
                        self.dcs_setup_result = None;
                        self.show_dcs_setup_popup = true;
                    }
                });
            });
            ui.separator();
            ui.add_enabled_ui(false, |ui| {
                self.shared_state.data.write().tune_ui(ui);
            });
        });

        // DCS setup confirmation popup
        if self.show_dcs_setup_popup {
            let dcs_path = dcs_saved_games_path();
            let path_display = dcs_path.display().to_string();
            egui::Window::new("Setup DCS")
                .collapsible(false)
                .resizable(false)
                .anchor(egui::Align2::CENTER_CENTER, [0.0, 0.0])
                .show(ui.ctx(), |ui| {
                    if let Some(ref result) = self.dcs_setup_result {
                        match result {
                            Ok(()) => {
                                ui.label("Directory created successfully.\nDon't forget to setup Moza Cockpit");
                            }
                            Err(err) => {
                                ui.colored_label(
                                    egui::Color32::RED,
                                    format!("Failed to create directory: {err}"),
                                );
                            }
                        }
                        if ui.button("OK").clicked() {
                            self.show_dcs_setup_popup = false;
                        }
                    } else {
                        ui.label(format!(
                            "Clicking yes will create the following directory on your disk:\n{path_display}"
                        ));
                        ui.horizontal(|ui| {
                            if ui.button("Yes").clicked() {
                                self.dcs_setup_result =
                                    Some(std::fs::create_dir_all(&dcs_path).map_err(|e| e.to_string()));
                            }
                            if ui.button("No").clicked() {
                                self.show_dcs_setup_popup = false;
                            }
                        });
                    }
                });
        }
    }
    fn update(&mut self, ui: &mut egui::Ui) {
        self.draw_ui(ui);
    }
}
impl eframe::App for App {
    fn ui(&mut self, ui: &mut egui::Ui, _frame: &mut eframe::Frame) {
        self.update(ui);
        if self.reactive {
            ui.request_repaint();
        }
    }
}
