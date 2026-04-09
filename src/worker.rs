use egui::mutex::Mutex;
use egui::mutex::RwLock;
use std::sync::Arc;

use std::net::{Ipv4Addr, SocketAddrV4};
use std::time::Duration;
use tokio::io::{AsyncBufReadExt, AsyncWriteExt, BufReader, Error};
use tokio::net::{TcpListener, TcpStream};

pub const MOZA_COCKPIT_DEFAULT_PORT: u16 = 3476;
pub const NUCLEAR_OPTION_DEFAULT_PORT: u16 = 3480;
use crate::model::MozaFFBData;

pub struct Worker {
    shared_state: Arc<SharedState>,
    game_stream: Option<BufReader<TcpStream>>,
    moza_stream: Option<TcpStream>,
    server: Option<TcpListener>,
    string_buffer: String,
}

impl Worker {
    pub fn new(shared_state: Arc<SharedState>) -> Self {
        Self {
            shared_state,
            game_stream: None,
            moza_stream: None,
            server: None,
            string_buffer: String::new(),
        }
    }
}

pub struct SharedState {
    pub server_online: ConnectionState,
    pub moza_online: ConnectionState,
    pub game_online: ConnectionState,
    pub ui_online: ConnectionState,
    pub moza_port: TcpPort,
    pub game_port: TcpPort,
    pub logs: LogState,
    pub data: RwLock<MozaFFBData>,
}
impl SharedState {
    pub fn new() -> Self {
        Self {
            server_online: Default::default(),
            moza_online: Default::default(),
            game_online: Default::default(),
            ui_online: ConnectionState(RwLock::new(true)),
            moza_port: TcpPort(Mutex::new((MOZA_COCKPIT_DEFAULT_PORT, true))),
            game_port: TcpPort(Mutex::new((NUCLEAR_OPTION_DEFAULT_PORT, true))),
            logs: LogState(Default::default()),
            data: Default::default(),
        }
    }
}
impl Default for SharedState {
    fn default() -> Self {
        Self::new()
    }
}
pub struct TcpPort(Mutex<(u16, bool)>);
#[derive(Default)]
pub struct ConnectionState(RwLock<bool>);
pub struct LogState(RwLock<Vec<String>>);

impl std::ops::Deref for LogState {
    type Target = RwLock<Vec<String>>;

    fn deref(&self) -> &Self::Target {
        &self.0
    }
}
impl ConnectionState {
    pub fn set_online(&self) {
        self.set_value(true)
    }
    pub fn set_offline(&self) {
        self.set_value(false)
    }
    fn set_value(&self, value: bool) {
        *self.0.write() = value;
    }
    pub fn online(&self) -> bool {
        *self.0.read()
    }
    pub fn offline(&self) -> bool {
        !*self.0.read()
    }
}
impl LogState {
    pub fn append_error(&self, message: impl std::fmt::Display) {
        self.0.write().push(format!("ERROR: {message}"));
    }
    pub fn append_info(&self, message: impl std::fmt::Display) {
        self.0.write().push(format!("INFO: {message}"));
    }
}
impl TcpPort {
    pub fn write(&self, port: u16) {
        let old_value = self.0.lock().0;
        if old_value != port {
            *self.0.lock() = (port, true);
        }
    }
    pub fn changed(&self) -> bool {
        self.0.lock().1
    }
    pub fn consume(&self) -> u16 {
        let mut locked = self.0.lock();
        let result = locked.0;
        *locked = (result, false);
        result
    }
    pub fn read(&self) -> u16 {
        self.0.lock().0
    }
}
pub enum UiCommand {
    Data(MozaFFBData),
    UiInfo(String),
    ServerKilled,
    ServerConnected,
    ServerFailed(Error),
    GameKilled,
    GameConnected,
    GameFailed(Error),
    MozaKilled,
    MozaConnected,
    MozaFailed(Error),
}
pub enum WorkerCommand {
    GamePort(u16),
    MozaPort(u16),
    Closed,
}

impl Worker {
    pub async fn run(&mut self) {
        loop {
            self.sync_from_ui().await;
            if self.shared_state.ui_online.offline() {
                log::info!("shutting down the worker");
                break;
            }
            // start the TCP server if the port has changed
            self.connect_to_moza().await;
            self.connect_to_game().await;
            self.forward_data().await;
        }
    }
    async fn sync_from_ui(&mut self) {
        if self.shared_state.moza_port.changed() || self.shared_state.game_port.changed() {
            self.disconnect_all();
        }
    }
    fn disconnect_all(&mut self) {
        self.disconnect_game();
        self.moza_stream = None;
        self.shared_state.moza_online.set_offline();
        self.server = None;
        self.shared_state.server_online.set_offline();
    }
    fn disconnect_game(&mut self) {
        self.game_stream = None;
        self.shared_state.game_online.set_offline();
    }
    fn debug_check_state(&self) {
        debug_assert_eq!(
            self.shared_state.server_online.online(),
            self.server.is_some()
        );
        debug_assert_eq!(
            self.shared_state.moza_online.online(),
            self.moza_stream.is_some()
        );
        debug_assert_eq!(
            self.shared_state.game_online.online(),
            self.game_stream.is_some()
        );
    }

    async fn connect_to_moza(&mut self) {
        self.debug_check_state();
        if self.server.is_some() {
            debug_assert!(self.moza_stream.is_some());
            return;
        }
        let moza_port = self.shared_state.moza_port.consume();
        let moza_ip = SocketAddrV4::new(Ipv4Addr::LOCALHOST, moza_port);
        match TcpListener::bind(moza_ip).await {
            Ok(listener) => {
                log::info!("server started on {moza_ip}");
                self.server = Some(listener);
                self.shared_state.server_online.set_online();
                self.shared_state
                    .logs
                    .append_info(format!("started tcp server on {moza_ip}"));
            }
            Err(err) => {
                log::error!("could not start server on {moza_ip}");
                self.shared_state.logs.append_error(err);
                self.disconnect_all();
                return;
            }
        }

        self.debug_check_state();
        assert!(self.moza_stream.is_none());
        assert!(self.game_stream.is_none());
        // this could block for a long time, so it must be always before we connect to the game
        // otherwise, we could block the game logic!
        match self.server.as_mut().unwrap().accept().await {
            Ok((stream, addr)) => {
                log::info!("moza app connect at {addr}");
                self.moza_stream = Some(stream);
                self.shared_state.moza_online.set_online();
                self.shared_state.logs.append_info("moza cockpit connected");
            }
            Err(err) => {
                log::error!("moza did not connect {err}");
                self.shared_state.logs.append_error(err);
                self.disconnect_all();
            }
        }
    }
    async fn connect_to_game(&mut self) {
        if self.server.is_none() || self.moza_stream.is_none() {
            return;
        }
        if self.game_stream.is_some() {
            return;
        }
        let game_ip = SocketAddrV4::new(Ipv4Addr::LOCALHOST, self.shared_state.game_port.consume());
        if let Ok(stream) = TcpStream::connect(game_ip).await {
            self.game_stream = Some(BufReader::new(stream));
            self.shared_state.game_online.set_online();
            log::info!("connected to the game stream");
            self.shared_state
                .logs
                .append_info("connected to game stream");
        } else {
            log::info!("could not connect to the game stream {}", game_ip);
            tokio::time::sleep(Duration::from_secs(1)).await;
        }
    }

    async fn forward_data(&mut self) {
        if self.game_stream.is_none() {
            return;
        }
        debug_assert!(self.server.is_some());
        debug_assert!(self.moza_stream.is_some());
        self.debug_check_state();

        self.string_buffer.clear();
        let data = match self
            .game_stream
            .as_mut()
            .unwrap()
            .read_line(&mut self.string_buffer)
            .await
        {
            Ok(num_bytes) => {
                if num_bytes == 0 {
                    self.shared_state.logs.append_info("game disconnected");
                    self.disconnect_game();
                    return;
                } else {
                    log::trace!("read data {}", self.string_buffer);
                    MozaFFBData::parse(&self.string_buffer)
                }
            }
            Err(err) => {
                self.shared_state.logs.append_error(err);
                self.disconnect_game();
                return;
            }
        };
        let message = data.to_string();
        *self.shared_state.data.write() = data;
        if let Err(err) = self
            .moza_stream
            .as_mut()
            .unwrap()
            .write_all(message.as_bytes())
            .await
        {
            log::error!("could not write to moza {err}");
            self.disconnect_all();
            self.shared_state.logs.append_error(err);
        }
    }
}
