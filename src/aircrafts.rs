use serde::{Deserialize, Serialize};

/// Represents the known aircraft modules in DCS World.
///
/// ### Naming & Telemetry Sources:
/// - **Telemetry Name**: Derived from the DCS internal `unit_type` string returned by `LoGetSelfData().Name`.
///   These names are used by Moza Pit House to identify the aircraft and apply specific FFB curves.
/// - **Sources**:
///   - [Hoggit World - DCS Unit Types](https://wiki.hoggitworld.com/view/DCS_unit_types)
///   - [VPforce TelemFFB Aircraft Mapping](https://docs.vpforce.eu/rhino/the-vpforce-telemffb-application/)
///   - Community reverse-engineering of `MOZA.lua` mapping tables.
#[derive(Clone, Debug, Serialize, Deserialize, Copy, Eq, PartialEq, Default)]
pub enum Aircraft {
    // Modern Fighters
    F16CViper,
    FA18CHornet,
    F15CEagle,
    F14BTomcat,
    JF17Thunder,

    // Attack / CAS
    #[default]
    A10C2Warthog,
    Su25TFrogfoot,
    AV8BNightAttack,

    // Helicopters
    AH64DApache,
    Ka50BlackShark3,
    Mi24PHind,
    UH1HHuey,
    SA342MGazelle,

    // Cold War / Reds
    MiG21bis,
    Su27Flanker,
    MiG29SFulcrum,
    F5ETigerII,

    // WWII
    SpitfireLFMkIX,
    P51DMustang,
    Bf109K4,
}

impl Aircraft {
    /// Returns the list of all supported aircraft.
    pub fn all() -> Vec<Aircraft> {
        vec![
            Aircraft::F16CViper,
            Aircraft::FA18CHornet,
            Aircraft::F15CEagle,
            Aircraft::F14BTomcat,
            Aircraft::JF17Thunder,
            Aircraft::A10C2Warthog,
            Aircraft::Su25TFrogfoot,
            Aircraft::AV8BNightAttack,
            Aircraft::AH64DApache,
            Aircraft::Ka50BlackShark3,
            Aircraft::Mi24PHind,
            Aircraft::UH1HHuey,
            Aircraft::SA342MGazelle,
            Aircraft::MiG21bis,
            Aircraft::Su27Flanker,
            Aircraft::MiG29SFulcrum,
            Aircraft::F5ETigerII,
            Aircraft::SpitfireLFMkIX,
            Aircraft::P51DMustang,
            Aircraft::Bf109K4,
        ]
    }

    /// Returns the internal name used by DCS for telemetry export.
    /// This is the string Moza Cockpit expects in the `aircraft_name` field.
    pub fn telemetry_name(&self) -> &'static str {
        match self {
            Aircraft::F16CViper => "F-16C_50",
            Aircraft::FA18CHornet => "FA-18C_hornet",
            Aircraft::F15CEagle => "F-15C",
            Aircraft::F14BTomcat => "F-14B",
            Aircraft::JF17Thunder => "JF-17",
            Aircraft::A10C2Warthog => "A-10C_2",
            Aircraft::Su25TFrogfoot => "Su-25T",
            Aircraft::AV8BNightAttack => "AV8BNA",
            Aircraft::AH64DApache => "AH-64D_BLK_II",
            Aircraft::Ka50BlackShark3 => "Ka-50_3",
            Aircraft::Mi24PHind => "Mi-24P",
            Aircraft::UH1HHuey => "UH-1H",
            Aircraft::SA342MGazelle => "SA342M",
            Aircraft::MiG21bis => "MiG-21bis",
            Aircraft::Su27Flanker => "Su-27",
            Aircraft::MiG29SFulcrum => "MiG-29S",
            Aircraft::F5ETigerII => "F-5E-3",
            Aircraft::SpitfireLFMkIX => "SpitfireLFMkIX",
            Aircraft::P51DMustang => "P-51D",
            Aircraft::Bf109K4 => "Bf-109K-4",
        }
    }

    /// Returns a friendly name for the User Interface.
    pub fn ui_name(&self) -> &'static str {
        match self {
            Aircraft::F16CViper => "F-16C Viper",
            Aircraft::FA18CHornet => "F/A-18C Hornet",
            Aircraft::F15CEagle => "F-15C Eagle",
            Aircraft::F14BTomcat => "F-14B Tomcat",
            Aircraft::JF17Thunder => "JF-17 Thunder",
            Aircraft::A10C2Warthog => "A-10C II Warthog",
            Aircraft::Su25TFrogfoot => "Su-25T Frogfoot",
            Aircraft::AV8BNightAttack => "AV-8B Night Attack",
            Aircraft::AH64DApache => "AH-64D Apache",
            Aircraft::Ka50BlackShark3 => "Ka-50 Black Shark 3",
            Aircraft::Mi24PHind => "Mi-24P Hind",
            Aircraft::UH1HHuey => "UH-1H Huey",
            Aircraft::SA342MGazelle => "SA342M Gazelle",
            Aircraft::MiG21bis => "MiG-21bis",
            Aircraft::Su27Flanker => "Su-27 Flanker",
            Aircraft::MiG29SFulcrum => "MiG-29S Fulcrum",
            Aircraft::F5ETigerII => "F-5E Tiger II",
            Aircraft::SpitfireLFMkIX => "Spitfire LF Mk. IX",
            Aircraft::P51DMustang => "P-51D Mustang",
            Aircraft::Bf109K4 => "Bf 109 K-4",
        }
    }

    pub fn from_telemetry_name(name: &str) -> Option<Aircraft> {
        Aircraft::all()
            .into_iter()
            .find(|aircraft| aircraft.telemetry_name() == name)
    }
}

impl std::fmt::Display for Aircraft {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}", self.telemetry_name())
    }
}
