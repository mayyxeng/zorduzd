use crate::worker::SharedState;
use crate::UiTunnable;
use std::sync::Arc;

pub struct App {
    shared_state: Arc<SharedState>,
    reactive: bool,
}

impl App {
    pub fn new(shared_state: Arc<SharedState>) -> Self {
        Self {
            shared_state,
            reactive: true,
        }
    }
    fn draw_ui(&mut self, ui: &mut egui::Ui) {
        egui::Panel::top("top").show_inside(ui, |ui| {
            ui.horizontal(|ui| {
                egui::widgets::global_theme_preference_buttons(ui);
                if ui
                    .selectable_label(!self.reactive, "▶ Continuous")
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
                    for (name, port, state) in [
                        ("server", None, server_online),
                        ("moza cockpit", moza_port, moza_online),
                        ("nuclear option", game_port, game_online),
                    ] {
                        ui.add(egui::Label::new(name));
                        if state {
                            ui.add(egui::Label::new(egui::RichText::new("online").strong()));
                        } else {
                            ui.add(egui::Label::new(egui::RichText::new("offline").weak()));
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
                    // ui.add(egui::Checkbox::new(&mut self.editable, "edit"));
                });
            });
            ui.separator();
            ui.add_enabled_ui(false, |ui| {
                self.shared_state.data.write().tune_ui(ui);
            });
        });
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
