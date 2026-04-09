pub mod aircrafts;
pub mod app;
pub mod model;
pub mod worker;

pub trait UiTunnable {
    fn tune_ui(&mut self, ui: &mut egui::Ui);
}
