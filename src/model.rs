use serde::{Deserialize, Serialize};

use crate::{aircrafts::Aircraft, UiTunnable};

/// Telemetry data structure used by Moza Cockpit to communicate with DCS World.
///
/// ### Data Sources & References:
/// - **DCS Export API**: Most fields are derived from the standard `Export.lua` environment.
///   Docs: [Hoggit World - DCS Export Script](https://wiki.hoggitworld.com/view/DCS_export_script)
/// - **Moza MOZA.lua**: Moza's proprietary export script which maps DCS functions to this format.
///   Location: `%USERPROFILE%\Saved Games\DCS\Scripts\MOZA\MOZA.lua`
/// - **Telemetry Units Reference**: Units (m/s, Radians, Gs) are confirmed via the VPforce documentation.
///   Docs: [VPforce TelemFFB Documentation](https://docs.vpforce.eu/rhino/the-vpforce-telemffb-application/#understanding-native-dcs-ffb-telemffb-and-vpforce-configurator)
#[derive(Clone, Debug, Serialize, Deserialize, Default)]
pub struct MozaFFBData {
    /// Type name of the aircraft (e.g., "A-10C", "F-16C_50").
    /// Source: `LoGetSelfData().Name`
    pub aircraft_name: Aircraft,

    /// Left engine RPM as a percentage.
    /// Range: 0.0 to 100.0 (Standard DCS) or 0.0 to 1.0.
    /// Source: `LoGetEngineInfo().RPM`
    pub engine_rpm_left: f32,

    /// Right engine RPM as a percentage.
    /// Range: 0.0 to 100.0 (Standard DCS) or 0.0 to 1.0.
    /// Source: `LoGetEngineInfo().RPM`
    pub engine_rpm_right: f32,

    /// Left landing gear extension state.
    /// Range: 0.0 (Up) to 1.0 (Down/Locked).
    /// Source: `LoGetSelfData().Gear`
    pub left_gear: f32,

    /// Nose landing gear extension state.
    /// Range: 0.0 (Up) to 1.0 (Down/Locked).
    pub nose_gear: f32,

    /// Right landing gear extension state.
    /// Range: 0.0 (Up) to 1.0 (Down/Locked).
    pub right_gear: f32,

    /// Longitudinal G-force (forward/back).
    /// Units: Gs.
    /// Source: `LoGetAccelerationUnits().x`
    pub acc_x: f32,

    /// Vertical G-force (up/down).
    /// Units: Gs.
    /// Source: `LoGetAccelerationUnits().y`
    pub acc_y: f32,

    /// Lateral G-force (left/right).
    /// Units: Gs.
    /// Source: `LoGetAccelerationUnits().z`
    pub acc_z: f32,

    /// Wind velocity component X.
    /// Units: m/s (Meters per Second).
    pub wind_x: f32,

    /// Wind velocity component Y.
    /// Units: m/s (Meters per Second).
    pub wind_y: f32,

    /// Wind velocity component Z.
    /// Units: m/s (Meters per Second).
    pub wind_z: f32,

    /// World velocity vector X.
    /// Units: m/s (Meters per Second).
    /// Source: `LoGetSelfData().Velocity.x`
    pub vector_velocity_x: f32,

    /// World velocity vector Y.
    /// Units: m/s (Meters per Second).
    /// Source: `LoGetSelfData().Velocity.y`
    pub vector_velocity_y: f32,

    /// World velocity vector Z.
    /// Units: m/s (Meters per Second).
    /// Source: `LoGetSelfData().Velocity.z`
    pub vector_velocity_z: f32,

    /// True Air Speed.
    /// Units: m/s (Meters per Second).
    /// Source: `LoGetTrueAirSpeed()`
    pub tas: f32,

    /// Indicated Air Speed.
    /// Units: m/s (Meters per Second).
    /// Source: `LoGetIndicatedAirSpeed()`
    pub ias: f32,

    /// Vertical speed (rate of climb/descent).
    /// Units: m/s (Meters per Second).
    /// Source: `LoGetVerticalVelocity()`
    pub vertical_velocity_speed: f32,

    /// Angle of Attack.
    /// Units: Radians.
    /// Source: `LoGetAngleOfAttack()`
    pub aoa: f32,

    /// True Heading.
    /// Units: Radians (0 to 2π).
    /// Source: `LoGetSelfData().Heading`
    pub heading: f32,

    /// Pitch angle.
    /// Units: Radians (-π/2 to π/2).
    /// Source: `LoGetSelfData().Pitch`
    pub pitch: f32,

    /// Bank (Roll) angle.
    /// Units: Radians (-π to π).
    /// Source: `LoGetSelfData().Bank`
    pub bank: f32,

    /// Angle of Sideslip.
    /// Units: Radians.
    /// Source: `LoGetAngleOfSideSlip()`
    pub aos: f32,

    /// Angular velocity component X (Roll Rate).
    /// Units: Radians/sec.
    pub euler_vx: f32,

    /// Angular velocity component Y (Pitch Rate).
    /// Units: Radians/sec.
    pub euler_vy: f32,

    /// Angular velocity component Z (Yaw Rate).
    /// Units: Radians/sec.
    pub euler_vz: f32,

    /// Canopy position.
    /// Range: 0.0 (Closed) to 1.0 (Fully Open).
    pub canopy_pos: f32,

    /// Flap position.
    /// Range: 0.0 (Retracted) to 1.0 (Fully Extended).
    pub flap_pos: f32,

    /// Gear lever position.
    /// Range: 0.0 (Up) to 1.0 (Down).
    pub gear_value: f32,

    /// Speed brake position.
    /// Range: 0.0 (Retracted) to 1.0 (Fully Extended).
    pub speedbrake_value: f32,

    /// Afterburner 1 active state.
    /// Range: 0.0 (Off) or 1.0 (On).
    pub afterburner_1: f32,

    /// Afterburner 2 active state.
    /// Range: 0.0 (Off) or 1.0 (On).
    pub afterburner_2: f32,

    /// Name of the currently selected/active weapon.
    pub weapon: String,

    /// Current flare count.
    pub flare: f32,

    /// Current chaff count.
    pub chaff: f32,

    /// Current cannon shell count.
    pub cannon_shells: u32,

    /// Current Mach number.
    /// Range: Dimensionless (e.g., 0.85).
    /// Source: `LoGetMachNumber()`
    pub mach: f32,

    /// Altitude above mean sea level.
    /// Units: Meters.
    /// Source: `LoGetAltitudeAboveSeaLevel()`
    pub h_above_sea_level: f32,

    /// A-10C Console LED state.
    /// Range: 0.0 or 1.0.
    /// Source: `LoGetIndication()`
    pub led_console: f32,

    /// A-10C Instrument result LED state.
    /// Range: 0.0 or 1.0.
    pub led_instruments_result: f32,

    /// A-10C APU Ready light state.
    /// Range: 0.0 or 1.0.
    pub light_apu_ready: f32,

    /// Landing gear warning light state.
    /// Range: 0.0 or 1.0.
    pub light_gear_warning: f32,

    /// Landing gear indicator light state.
    /// Range: 0.0 or 1.0.
    pub light_gear_indicator: f32,
}
impl MozaFFBData {
    pub fn parse(s: &str) -> Self {
        let mut data = Self::default();
        for pair in s.split(';') {
            if pair.is_empty() {
                continue;
            }
            let mut parts = pair.split(',');
            let key = parts.next().unwrap_or("").trim();
            let value = parts.next().unwrap_or("").trim();

            match key {
                "aircraft_name" => {
                    if let Some(a) = Aircraft::from_telemetry_name(value) {
                        data.aircraft_name = a;
                    }
                }
                "engine_rpm_left" => data.engine_rpm_left = value.parse().unwrap_or(0.0),
                "engine_rpm_right" => data.engine_rpm_right = value.parse().unwrap_or(0.0),
                "left_gear" => data.left_gear = value.parse().unwrap_or(0.0),
                "nose_gear" => data.nose_gear = value.parse().unwrap_or(0.0),
                "right_gear" => data.right_gear = value.parse().unwrap_or(0.0),
                "acc_x" => data.acc_x = value.parse().unwrap_or(0.0),
                "acc_y" => data.acc_y = value.parse().unwrap_or(0.0),
                "acc_z" => data.acc_z = value.parse().unwrap_or(0.0),
                "wind_x" => data.wind_x = value.parse().unwrap_or(0.0),
                "wind_y" => data.wind_y = value.parse().unwrap_or(0.0),
                "wind_z" => data.wind_z = value.parse().unwrap_or(0.0),
                "vector_velocity_x" => data.vector_velocity_x = value.parse().unwrap_or(0.0),
                "vector_velocity_y" => data.vector_velocity_y = value.parse().unwrap_or(0.0),
                "vector_velocity_z" => data.vector_velocity_z = value.parse().unwrap_or(0.0),
                "tas" => data.tas = value.parse().unwrap_or(0.0),
                "ias" => data.ias = value.parse().unwrap_or(0.0),
                "vertical_velocity_speed" => {
                    data.vertical_velocity_speed = value.parse().unwrap_or(0.0)
                }
                "aoa" => data.aoa = value.parse().unwrap_or(0.0),
                "heading" => data.heading = value.parse().unwrap_or(0.0),
                "pitch" => data.pitch = value.parse().unwrap_or(0.0),
                "bank" => data.bank = value.parse().unwrap_or(0.0),
                "aos" => data.aos = value.parse().unwrap_or(0.0),
                "euler_vx" => data.euler_vx = value.parse().unwrap_or(0.0),
                "euler_vy" => data.euler_vy = value.parse().unwrap_or(0.0),
                "euler_vz" => data.euler_vz = value.parse().unwrap_or(0.0),
                "canopy_pos" => data.canopy_pos = value.parse().unwrap_or(0.0),
                "flap_pos" => data.flap_pos = value.parse().unwrap_or(0.0),
                "gear_value" => data.gear_value = value.parse().unwrap_or(0.0),
                "speedbrake_value" => data.speedbrake_value = value.parse().unwrap_or(0.0),
                "afterburner_1" => data.afterburner_1 = value.parse().unwrap_or(0.0),
                "afterburner_2" => data.afterburner_2 = value.parse().unwrap_or(0.0),
                "weapon" => data.weapon = value.to_string(),
                "flare" => data.flare = value.parse().unwrap_or(0.0),
                "chaff" => data.chaff = value.parse().unwrap_or(0.0),
                "cannon_shells" => data.cannon_shells = value.parse().unwrap_or(0),
                "mach" => data.mach = value.parse().unwrap_or(0.0),
                "h_above_sea_level" => data.h_above_sea_level = value.parse().unwrap_or(0.0),
                "led_console" => data.led_console = value.parse().unwrap_or(0.0),
                "led_instruments_result" => {
                    data.led_instruments_result = value.parse().unwrap_or(0.0)
                }
                "light_apu_ready" => data.light_apu_ready = value.parse().unwrap_or(0.0),
                "light_gear_warning" => data.light_gear_warning = value.parse().unwrap_or(0.0),
                "light_gear_indicator" => data.light_gear_indicator = value.parse().unwrap_or(0.0),
                _ => {} // Ignore unknown fields
            }
        }
        data
    }

    fn tune_in_grid(&mut self, ui: &mut egui::Ui) {
        let Self {
            aircraft_name,
            engine_rpm_left,
            engine_rpm_right,
            left_gear,
            nose_gear,
            right_gear,
            acc_x: acceleration_x,
            acc_y: acceleration_y,
            acc_z: acceleration_z,
            wind_x,
            wind_y,
            wind_z,
            vector_velocity_x,
            vector_velocity_y,
            vector_velocity_z,
            tas,
            ias,
            vertical_velocity_speed,
            aoa,
            heading,
            pitch,
            bank,
            aos,
            euler_vx,
            euler_vy,
            euler_vz,
            canopy_pos,
            flap_pos,
            gear_value,
            speedbrake_value,
            afterburner_1,
            afterburner_2,
            weapon,
            flare,
            chaff,
            cannon_shells,
            mach,
            h_above_sea_level: altitude_sea_level,
            led_console,
            led_instruments_result,
            light_apu_ready,
            light_gear_warning,
            light_gear_indicator,
        } = self;
        ui.spacing_mut().slider_width = 50.0;
        ui.label("Aircraft");
        egui::ComboBox::from_id_salt("selected aircraft")
            .selected_text(aircraft_name.ui_name())
            .show_ui(ui, |ui| {
                for aircraft in Aircraft::all() {
                    ui.selectable_value(aircraft_name, aircraft, aircraft.ui_name());
                }
            });
        ui.end_row();

        ui.label("Engine RPM (L/R)");
        ui.add(
            egui::DragValue::new(engine_rpm_left)
                .range(0.0..=100.0)
                .suffix("%"),
        );
        ui.add(
            egui::DragValue::new(engine_rpm_right)
                .range(0.0..=100.0)
                .suffix("%"),
        );
        ui.end_row();

        ui.label("Landing Gear (L/N/R)");
        ui.add(egui::Slider::new(left_gear, 0.0..=1.0).show_value(false));
        ui.add(egui::Slider::new(nose_gear, 0.0..=1.0).show_value(false));
        ui.add(egui::Slider::new(right_gear, 0.0..=1.0).show_value(false));
        ui.end_row();

        ui.label("Acceleration (X/Y/Z)");
        ui.add(egui::DragValue::new(acceleration_x).speed(0.1).suffix("G"));
        ui.add(egui::DragValue::new(acceleration_y).speed(0.1).suffix("G"));
        ui.add(egui::DragValue::new(acceleration_z).speed(0.1).suffix("G"));
        ui.end_row();

        ui.label("Wind (X/Y/Z)");
        ui.add(egui::DragValue::new(wind_x).suffix("m/s"));
        ui.add(egui::DragValue::new(wind_y).suffix("m/s"));
        ui.add(egui::DragValue::new(wind_z).suffix("m/s"));
        ui.end_row();

        ui.label("Velocity (X/Y/Z)");
        ui.add(egui::DragValue::new(vector_velocity_x).suffix("m/s"));
        ui.add(egui::DragValue::new(vector_velocity_y).suffix("m/s"));
        ui.add(egui::DragValue::new(vector_velocity_z).suffix("m/s"));
        ui.end_row();

        ui.label("Airspeed (TAS/IAS)");
        ui.add(egui::DragValue::new(tas).suffix("m/s (TAS)"));
        ui.add(egui::DragValue::new(ias).suffix("m/s (IAS)"));
        ui.end_row();

        ui.label("Vertical Speed");
        ui.add(egui::DragValue::new(vertical_velocity_speed).suffix("m/s"));
        ui.end_row();

        ui.label("Angles (AoA/AoS)");
        ui.add(egui::DragValue::new(aoa).speed(0.01).suffix("rad (AoA)"));
        ui.add(egui::DragValue::new(aos).speed(0.01).suffix("rad (AoS)"));
        ui.end_row();

        ui.label("Orientation (H/P/B)");
        ui.add(egui::DragValue::new(heading).speed(0.01).suffix("rad (H)"));
        ui.add(egui::DragValue::new(pitch).speed(0.01).suffix("rad (P)"));
        ui.add(egui::DragValue::new(bank).speed(0.01).suffix("rad (B)"));
        ui.end_row();

        ui.label("Angular Vel (R/P/Y)");
        ui.add(egui::DragValue::new(euler_vx).speed(0.01).suffix("rad/s"));
        ui.add(egui::DragValue::new(euler_vy).speed(0.01).suffix("rad/s"));
        ui.add(egui::DragValue::new(euler_vz).speed(0.01).suffix("rad/s"));
        ui.end_row();

        ui.label("Mechanics (C/F/G/S)");
        ui.add(
            egui::Slider::new(canopy_pos, 0.0..=1.0)
                .show_value(false)
                .text("Canopy"),
        );
        ui.add(
            egui::Slider::new(flap_pos, 0.0..=1.0)
                .show_value(false)
                .text("Flaps"),
        );
        ui.add(
            egui::Slider::new(gear_value, 0.0..=1.0)
                .show_value(false)
                .text("Gear"),
        );
        ui.add(
            egui::Slider::new(speedbrake_value, 0.0..=1.0)
                .show_value(false)
                .text("S.Brake"),
        );
        ui.end_row();

        ui.label("Afterburners (1/2)");
        ui.add(egui::Slider::new(afterburner_1, 0.0..=1.0).show_value(false));
        ui.add(egui::Slider::new(afterburner_2, 0.0..=1.0).show_value(false));
        ui.end_row();

        ui.label("Weapon System");
        ui.add(egui::TextEdit::singleline(weapon).desired_width(100.0));
        ui.add(
            egui::DragValue::new(cannon_shells)
                .range(0.0..=10000.0)
                .suffix(" shells"),
        );
        ui.end_row();

        ui.label("Countermeasures (F/C)");
        ui.add(
            egui::DragValue::new(flare)
                .range(0.0..=10000.0)
                .suffix(" flares"),
        );
        ui.add(egui::DragValue::new(chaff).suffix(" chaff"));
        ui.end_row();

        ui.label("Mach / Altitude");
        ui.add(egui::DragValue::new(mach).speed(0.01).prefix("M"));
        ui.add(egui::DragValue::new(altitude_sea_level).suffix(" m"));
        ui.end_row();

        ui.label("A-10C LEDs & Lights");
        ui.add(
            egui::Slider::new(led_console, 0.0..=1.0)
                .show_value(false)
                .text("Console"),
        );
        ui.add(
            egui::Slider::new(led_instruments_result, 0.0..=1.0)
                .show_value(false)
                .text("Instr"),
        );
        ui.add(
            egui::Slider::new(light_apu_ready, 0.0..=1.0)
                .show_value(false)
                .text("APU"),
        );
        ui.add(
            egui::Slider::new(light_gear_warning, 0.0..=1.0)
                .show_value(false)
                .text("G.Warn"),
        );
        ui.add(
            egui::Slider::new(light_gear_indicator, 0.0..=1.0)
                .show_value(false)
                .text("G.Ind"),
        );
        ui.end_row();
    }
}
impl UiTunnable for MozaFFBData {
    fn tune_ui(&mut self, ui: &mut egui::Ui) {
        egui::ScrollArea::both()
            .min_scrolled_width(500.0)
            .min_scrolled_height(500.0)
            .show(ui, |ui| {
                egui::Grid::new("Model")
                    .num_columns(6)
                    .spacing([20.0, 10.0])
                    .min_col_width(80.0)
                    .max_col_width(80.0)
                    .striped(true)
                    .show(ui, |ui| self.tune_in_grid(ui))
            });
    }
}

/// A handy macro to make a string representation of [`MozaFFBData`]. Used only in this module.
macro_rules! emit_fields {
    ($($field: ident,)*) => {
        fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
            $(write!(f, "{},{};", stringify!($field), self.$field)?;)*
            Ok(())
        }
    };
}
/// String representation of the data.
impl std::fmt::Display for MozaFFBData {
    emit_fields! {
        aircraft_name,
        engine_rpm_left,
        engine_rpm_right,
        left_gear,
        nose_gear,
        right_gear,
        acc_x,
        acc_y,
        acc_z,
        wind_x,
        wind_y,
        wind_z,
        vector_velocity_x,
        vector_velocity_y,
        vector_velocity_z,
        tas,
        ias,
        vertical_velocity_speed,
        aoa,
        heading,
        pitch,
        bank,
        aos,
        euler_vx,
        euler_vy,
        euler_vz,
        canopy_pos,
        flap_pos,
        gear_value,
        speedbrake_value,
        afterburner_1,
        afterburner_2,
        weapon,
        flare,
        chaff,
        cannon_shells,
        mach,
        h_above_sea_level,
        led_console,
        led_instruments_result,
        light_apu_ready,
        light_gear_warning,
        light_gear_indicator,
    }
}
