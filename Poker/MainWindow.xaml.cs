﻿using System.Linq;

namespace Cards
{
    using NetworkManager;
    using Newtonsoft.Json;
    using Solitare;
    using Solitare.Game;
    using Solitare.Network;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using System.Timers;
    using System.Windows;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        #region Constants and Fields

        GameState lastGameState;
        Settings _savedSettings;

        /// <summary>
        /// The DependencyProperty that backs the Stack property.
        /// </summary>
        public static readonly DependencyProperty Player1Property = DependencyProperty.Register(
            "FirstPlayer", typeof(Player), typeof(MainWindow), new FrameworkPropertyMetadata(null));
        public static readonly DependencyProperty Player2Property = DependencyProperty.Register(
            "SecondPlayer", typeof(Player), typeof(MainWindow), new FrameworkPropertyMetadata(null));
        public static readonly DependencyProperty Player3Property = DependencyProperty.Register(
            "ThirdPlayer", typeof(Player), typeof(MainWindow), new FrameworkPropertyMetadata(null));
        public static readonly DependencyProperty Player4Property = DependencyProperty.Register(
            "FourthPlayer", typeof(Player), typeof(MainWindow), new FrameworkPropertyMetadata(null));
        public static readonly DependencyProperty Player5Property = DependencyProperty.Register(
            "FifthPlayer", typeof(Player), typeof(MainWindow), new FrameworkPropertyMetadata(null));
        public static readonly DependencyProperty Player6Property = DependencyProperty.Register(
            "SixthPlayer", typeof(Player), typeof(MainWindow), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty RotationProperty = DependencyProperty.Register(
           "RotationState", typeof(PlayerRotationState), typeof(MainWindow), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty FlopProp = DependencyProperty.Register(
            "Flop", typeof(ObservableCollection<Card>), typeof(MainWindow), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty PendingPotProp = DependencyProperty.Register(
            "PendingPot", typeof(int), typeof(MainWindow), new FrameworkPropertyMetadata(null));
        public static readonly DependencyProperty PotProp = DependencyProperty.Register(
            "Pot", typeof(int), typeof(MainWindow), new FrameworkPropertyMetadata(null));
        public static readonly DependencyProperty IsServerProp = DependencyProperty.Register(
            "IsServer", typeof(bool), typeof(MainWindow), new FrameworkPropertyMetadata(null));
        public static readonly DependencyProperty InGameProp = DependencyProperty.Register(
            "InGame", typeof(bool), typeof(MainWindow), new FrameworkPropertyMetadata(null));
        public static readonly DependencyProperty IsAdminingProp = DependencyProperty.Register(
            "IsAdmining", typeof(bool), typeof(MainWindow), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty NameProp = DependencyProperty.Register(
            "PlayerName", typeof(string), typeof(MainWindow), new FrameworkPropertyMetadata(null));
        public static readonly DependencyProperty ServerAddressProp = DependencyProperty.Register(
            "ServerAddress", typeof(string), typeof(MainWindow), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty PlayerIdxProp = DependencyProperty.Register(
            "PlayerIdx", typeof(string), typeof(MainWindow), new FrameworkPropertyMetadata(null));
        public static readonly DependencyProperty PointsProp = DependencyProperty.Register(
            "Points", typeof(string), typeof(MainWindow), new FrameworkPropertyMetadata(null));

        private Stack<Card> _deck;

        private static Guid MyUuid = Guid.NewGuid();
        private Client Client;
        private Server Server;
        private Logger ServerLogger;

        private int Round = 0;

        private Timer UpdateTimer;

        //These keep track of where cards were drawn from so they can be easily replaced
        private int _hitStackX;
        private int _hitStackY;
        private int _topStack;
        private int _numToDeal = 3;
        private bool _pickedLast = false;
        #endregion

        #region Constructors and Destructors

        public MainWindow()
        {
            InitializeComponent();

            FirstPlayer = new Player("Test", 1, MyUuid, true);
            SecondPlayer = new Player(2);
            ThirdPlayer = new Player(3);
            FourthPlayer = new Player(4);
            FifthPlayer = new Player(5);
            SixthPlayer = new Player(6);

            _savedSettings = Settings.Load();

            if (_savedSettings != null)
            {
                PlayerName = _savedSettings.LastUsedName;
                ServerAddress = _savedSettings.LastUsedServer;
                RotationState = _savedSettings.Rotation;
            }

            Pot = 0;
            PendingPot = 0;

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            InGame = false;

#if DEBUG

            string[] args = Environment.GetCommandLineArgs();

            var cmdArgs = new Dictionary<string, string>();

            for (int index = 1; index < args.Length; index += 2)
            {
                cmdArgs.Add(args[index], args[index + 1]);
            }

            const string hostKey = "-h";
            const string nameKey = "-n";
            const string connectKey = "-c";

            ServerAddress = "127.0.0.1:2015";
            PlayerName = "foo";

            if (cmdArgs.ContainsKey(nameKey))
            {
                PlayerName = cmdArgs[nameKey];
            }

            // -h.\Poker\bin\Debug\Poker.exe -n Server -h 2015 means host on this port
            if (cmdArgs.ContainsKey(hostKey))
            {
                ServerAddress = cmdArgs[hostKey];
                HostClick(null, null);
            }

            // -c means connect to this address:port
            if (cmdArgs.ContainsKey(connectKey))
            {
                ServerAddress = cmdArgs[connectKey];
                JoinClick(null, null);

            }
#endif

        }

        #endregion

        #region Public Properties

        public ObservableCollection<Card> Flop
        {
            get
            {
                return (ObservableCollection<Card>)GetValue(FlopProp);
            }
            set
            {
                SetValue(FlopProp, value);
            }
        }

        public PlayerRotationState RotationState
        {
            get
            {
                return (PlayerRotationState)GetValue(RotationProperty);
            }
            set
            {
                SetValue(RotationProperty, value);
            }
        }

        public string Points
        {
            get
            {
                return (string)GetValue(PointsProp);
            }
            set
            {
                SetValue(PointsProp, value);
            }
        }

        public string PlayerIdx
        {
            get
            {
                return (string)GetValue(PlayerIdxProp);
            }
            set
            {
                SetValue(PlayerIdxProp, value);
            }
        }

        public string PlayerName
        {
            get
            {
                return (string)GetValue(NameProp);
            }
            set
            {
                SetValue(NameProp, value);
            }
        }

        public string ServerAddress
        {
            get
            {
                return (string)GetValue(ServerAddressProp);
            }
            set
            {
                SetValue(ServerAddressProp, value);
            }
        }

        public bool IsServer
        {
            get
            {
                return (bool)GetValue(IsServerProp);
            }
            set
            {
                SetValue(IsServerProp, value);
            }
        }

        public bool InGame
        {
            get
            {
                return (bool)GetValue(InGameProp);
            }
            set
            {
                SetValue(InGameProp, value);
            }
        }

        public bool IsAdmining
        {
            get
            {
                return (bool)GetValue(IsAdminingProp);
            }
            set
            {
                SetValue(IsAdminingProp, value);
            }
        }

        public int PendingPot
        {
            get
            {
                return (int)GetValue(PendingPotProp);
            }
            set
            {
                SetValue(PendingPotProp, value);
            }
        }

        public int Pot
        {
            get
            {
                return (int)GetValue(PotProp);
            }
            set
            {
                SetValue(PotProp, value);
            }
        }

        public Player FirstPlayer
        {
            get
            {
                return (Player)GetValue(Player1Property);
            }
            set
            {
                SetValue(Player1Property, value);
            }
        }

        public Player SecondPlayer
        {
            get
            {
                return (Player)GetValue(Player2Property);
            }
            set
            {
                SetValue(Player2Property, value);
            }
        }

        public Player ThirdPlayer
        {
            get
            {
                return (Player)GetValue(Player3Property);
            }
            set
            {
                SetValue(Player3Property, value);
            }
        }

        public Player FourthPlayer
        {
            get
            {
                return (Player)GetValue(Player4Property);
            }
            set
            {
                SetValue(Player4Property, value);
            }
        }

        public Player FifthPlayer
        {
            get
            {
                return (Player)GetValue(Player5Property);
            }
            set
            {
                SetValue(Player5Property, value);
            }
        }

        public Player SixthPlayer
        {
            get
            {
                return (Player)GetValue(Player6Property);
            }
            set
            {
                SetValue(Player6Property, value);
            }
        }

        #endregion

        #region PlayerMethods

        public void UpdatePlayer(Player player)
        {
            switch (player.Index)
            {
                default:
                case 1:
                    FirstPlayer = player;
                    break;
                case 2:
                    SecondPlayer = player;
                    break;
                case 3:
                    ThirdPlayer = player;
                    break;
                case 4:
                    FourthPlayer = player;
                    break;
                case 5:
                    FifthPlayer = player;
                    break;
                case 6:
                    SixthPlayer = player;
                    break;
            }
        }

        public Player GetPlayer(Guid uuid)
        {
            return GetAllPlayers().Where(p => p.Uuid == uuid).FirstOrDefault();
        }

        public Player GetPlayer(int idx)
        {
            return GetAllPlayers().Where(p => p.Index == idx).FirstOrDefault();
        }

        public IEnumerable<Player> GetAllPlayers(bool includeFakes = false)
        {
            var allPlayers = new List<Player>();
            allPlayers.Add(FirstPlayer);
            allPlayers.Add(SecondPlayer);
            allPlayers.Add(ThirdPlayer);
            allPlayers.Add(FourthPlayer);
            allPlayers.Add(FifthPlayer);
            allPlayers.Add(SixthPlayer);

            return allPlayers.Where(pl => !pl.IsFake || includeFakes);
        }

        #endregion

        private void JoinClick(object sender, RoutedEventArgs e)
        {
            if (sender != null)
            {
                _savedSettings = new Settings()
                {
                    LastUsedName = PlayerName,
                    LastUsedServer = ServerAddress,
                    Rotation = _savedSettings?.Rotation ?? PlayerRotationState.All
                };

                Settings.Save(_savedSettings);
            }

            Client = new Client(ClientHandleMessage);

            this.Title += " (Client)";

            var parts = ServerAddress.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int port))
            {
                return;
            }

            if (Client.Connect(parts[0], port))
            {
                var newMessage = new Message() { Type = MessageType.Join, Value = PlayerName, Uuid = MyUuid };
                var srl = JsonConvert.SerializeObject(newMessage);

                Client.Send(srl);
                Console.WriteLine("Sent");
            }
            else
            {
                throw new Exception("failed to connect");
            }

            FirstPlayer = new Player(PlayerName, 1, MyUuid, true);
            InGame = true;
        }

        private void HostClick(object sender, RoutedEventArgs e)
        {
#if DEBUG
            // handle default value of 127.0.0.0:1000 for easy testing
            if (ServerAddress.Contains(':'))
            {
                ServerAddress = ServerAddress.Split(':')[1];
            }

#endif
            if (sender != null)
            {
                _savedSettings = new Settings()
                {
                    LastUsedName = PlayerName,
                    LastUsedServer = ServerAddress,
                    Rotation = _savedSettings?.Rotation ?? PlayerRotationState.All
                };

                Settings.Save(_savedSettings);
            }


            if (!int.TryParse(ServerAddress, out int port))
            {
                return;
            }

            this.Title += $" (Host : {port})";

            ServerLogger = new Logger("out.log");

            Task.Factory.StartNew(() =>
            {
                Server = new Server(ServerHandleMessage, GetAcknowledgementResponseAsString);
                Server.StartListening(port);
            });

            FirstPlayer = new Player(PlayerName + " (Host)", 1, MyUuid, true);
            InGame = true;
            IsServer = true;
        }

        private void root_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!this.IsServer && this.InGame)
                {
                    var newMessage = new Message() { Type = MessageType.Leave, Value = PlayerName, Uuid = MyUuid };
                    var srl = JsonConvert.SerializeObject(newMessage);

                    Client.Send(srl);
                    Console.WriteLine("Leaving");
                }
            }));
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.ExceptionObject.ToString(), e.ToString());
        }

        public void LogGameState()
        {
            ServerLogger?.Log(GetCurrentGameStateAsString());
        }

        #region ConfigMethods

        private void SetBet(object sender, RoutedEventArgs e)
        {
            try
            {
                var idx = int.Parse(PlayerIdx);
                var points = int.Parse(Points);

                var player = GetPlayer(idx);
                if (player != default(Player))
                {
                    player.BetForGame = points;
                }

                this.Pot = GetAllPlayers().Select(p => p.BetForGame).Sum();
                IsAdmining = false;
                InGame = true;
                SendUpdatedGameState();
            }
            catch { }
        }

        private void SetPendingBet(object sender, RoutedEventArgs e)
        {
            try
            {
                var idx = int.Parse(PlayerIdx);
                var points = int.Parse(Points);

                var player = GetPlayer(idx);
                if (player != default(Player))
                {
                    player.BetForRound = points;
                }

                if (points > this.PendingPot)
                {
                    this.PendingPot = points;
                }

                IsAdmining = false;
                InGame = true;
                SendUpdatedGameState();
            }
            catch { }
        }

        private void Config(object sender, RoutedEventArgs e)
        {
            IsAdmining = true;
            InGame = false;
        }

        private void SetPoints(object sender, RoutedEventArgs e)
        {
            try
            {
                var idx = int.Parse(PlayerIdx);
                var points = int.Parse(Points);

                var player = GetPlayer(idx);
                if (player != default(Player))
                {
                    player.Points = points;
                    UpdatePlayer(player);
                }

                IsAdmining = false;
                InGame = true;
                SendUpdatedGameState();
            }
            catch { }
        }

        private void EndAdmin(object sender, RoutedEventArgs e)
        {
            IsAdmining = false;
            InGame = true;
        }

        #endregion
        
        #region Communication

        bool ClientHandleMessage(string messageStr)
        {
            return Dispatcher.Invoke(new Func<bool>(() =>
            {
                if (this.IsServer)
                {
                    // Noop
                }
                else
                {
                    try
                    {
                        var serverMessage = JsonConvert.DeserializeObject<ServerMessage>(messageStr);

                        switch (serverMessage.Type)
                        {
                            default:
                            case ServerMessageType.Ack:
                                break;
                            case ServerMessageType.Update:
                                var gamestate = JsonConvert.DeserializeObject<GameState>(serverMessage.Value);

                                this.Flop = gamestate.Flop != null ? new ObservableCollection<Card>(gamestate.Flop) : null;

                                this.Pot = gamestate.Pot;
                                this.PendingPot = gamestate.PendingPot;

                                var idxOff = 0;

                                foreach (var player in gamestate.Players)
                                {
                                    if (player.Uuid == MyUuid)
                                    {
                                        idxOff = 1 - player.Index;
                                        player.ShowHand();
                                    }
                                    else if (!player.IsShowingHand)
                                    {
                                        player.HideHand();
                                    }
                                }

                                foreach (var player in gamestate.Players)
                                {
                                    var tempIdx = (player.Index + idxOff);
                                    if (tempIdx <= 0)
                                    {
                                        tempIdx += gamestate.Players.Length;
                                    }

                                    player.Index = tempIdx;

                                    // Don't overwrite my pending state if it's my turn, unless we're in a new round
                                    if (player.Uuid == MyUuid && player.IsMyTurn && gamestate.Round <= lastGameState.Round && gamestate.Pot == lastGameState.Pot)
                                    {
                                        player.BetForRound = FirstPlayer.BetForRound;
                                    }

                                    player.IsMe = player.Uuid == MyUuid;

                                    UpdatePlayer(player);
                                }

                                lastGameState = gamestate;
                                break;
                        }
                    }
                    catch { }
                }

                return true;

            }));
        }

        bool ServerHandleMessage(string messageStr)
        {
            return Dispatcher.Invoke(new Func<bool>(() =>
            {
                if (this.IsServer)
                {
                    try
                    {
                        var message = JsonConvert.DeserializeObject<Message>(messageStr);

                        switch (message.Type)
                        {
                            case MessageType.PollState:
                            default:
                                break;
                            case MessageType.DeclareWinner:
                                HandleDeclareWinner(message.Uuid, message.Value);
                                break;
                            case MessageType.Join:
                                PlayerJoin(message.Value, message.Uuid);
                                break;
                            case MessageType.Bet:
                                PlayerBet(message.Value, message.Uuid);
                                break;
                            case MessageType.AdvanceFlop:
                                var player = GetPlayer(message.Uuid);
                                if (player.IsDealer)
                                {
                                    AdvanceFlop();
                                }
                                break;
                            case MessageType.Fold:
                            case MessageType.ShowHand:
                                PlayerAct(message.Type, message.Uuid);
                                break;
                        }

                        SendUpdatedGameState();
                    }
                    catch
                    {
                        // TODO: error handling hehe
                    }
                }
                else
                {
                    //no op
                }

                return true;

            }));
        }

        private string GetAcknowledgementResponseAsString()
        {
            var ackMsg = new ServerMessage()
            {
                Type = ServerMessageType.Ack
            };

            return JsonConvert.SerializeObject(ackMsg);
        }

        private string GetCurrentGameStateAsString()
        {
            try
            {
                return Dispatcher.Invoke(new Func<string>(() =>
                {
                    return JsonConvert.SerializeObject(GetCurrentGameState());
                }));
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
        }

        private GameState GetCurrentGameState()
        {
            try
            {
                var state = new GameState()
                {
                    Players = GetAllPlayers()?.ToArray(),
                    Flop = Flop?.ToArray(),
                    Pot = Pot,
                    PendingPot = PendingPot,
                    Round = Round
                };

                return state;

            }
            catch (Exception ex)
            {
                return null;
            }
        }


        private void SendUpdatedGameState()
        {
            if (this.IsServer)
            {
                var gameStateMessage = new ServerMessage()
                {
                    Type = ServerMessageType.Update,
                    Value = GetCurrentGameStateAsString()
                };

                var serializedStateMessage = JsonConvert.SerializeObject(gameStateMessage);

                this.Server.PushToListeners(serializedStateMessage);
            }
        }

        #endregion

        #region ServerCallbacks

        public void PlayerJoin(string name, Guid uuid)
        {

            foreach (var player in GetAllPlayers(true))
            {
                if (player.IsFake)
                {
                    var newPlayer = new Player(name, player.Index, uuid);
                    UpdatePlayer(newPlayer);
                    break;
                }
            }
        }

        public void PlayerAct(MessageType type, Guid uuid)
        {
            var player = GetPlayer(uuid);
            if (player.IsMyTurn || type == MessageType.ShowHand)
            {
                switch (type)
                {
                    case MessageType.Fold:
                        player.IsOut = true;
                        AdvanceTurn();
                        break;
                    case MessageType.ShowHand:
                        player.ShowHand();
                        break;
                }
            }
        }

        public void HandleDeclareWinner(Guid sender, string winnerUuidStr)
        {
            if (Guid.TryParse(winnerUuidStr, out Guid winnerUuid))
            {
                var player = GetPlayer(sender);
                if (player.IsDealer)
                {
                    var winner = GetPlayer(winnerUuid);
                    if (!winner.IsFake)
                    {
                        CommitAllBets();
                        winner.Points += this.Pot;
                        PassDealer_Click(null, null);
                    }
                }
            }
        }

        public void PlayerBet(string bet, Guid uuid)
        {
            if (int.TryParse(bet, out int betInt))
            {
                var player = GetPlayer(uuid);

                if (player.IsMyTurn)
                {
                    if (betInt > PendingPot)
                    {
                        PendingPot = betInt;
                    }
                    else if (betInt < PendingPot && player.Points > betInt)
                    {
                        // If we're trying to bet lower than pending bet and we have more points to bet with, don't allow change
                        return;
                    }

                    var dif = betInt - player.BetForRound;
                    player.BetForRound = betInt;
                    player.Points -= dif;
                    AdvanceTurn();
                }
            }
        }

        #endregion

        #region GameStateModifiers

        public int AdvanceDealer()
        {
            var toSet = false;
            var didSet = false;
            var newDealerIndex = -1;

            foreach (var player in GetAllPlayers())
            {
                if (toSet && !player.IsOut)
                {
                    player.IsDealer = true;
                    didSet = true;
                    newDealerIndex = player.Index;
                    break;
                }
                else if (player.IsDealer)
                {
                    player.IsDealer = false;
                    toSet = true;
                }
            }

            if (!didSet && toSet)
            {
                foreach (var player in GetAllPlayers())
                {
                    if (!player.IsOut)
                    {
                        player.IsDealer = true;
                        newDealerIndex = player.Index;
                        break;
                    }
                }
            }

            // If we are the server, this will push updated state to cilents
            SendUpdatedGameState();
            return newDealerIndex;
        }

        private void NewDeal(object sender, RoutedEventArgs e)
        {
            foreach (var player in GetAllPlayers())
            {
                player.IsDealer = false;
                player.IsMyTurn = false;
                player.Points = 500;
                player.BetForRound = 0;
                player.BetForGame = 0;
            }

            FirstPlayer.IsDealer = true;
            FirstPlayer.IsMyTurn = true;

            ResetHand_Click(null, null);

            // Pass off to small blind
            AdvanceTurn();
        }

        // Advance the turn to the provided index (or next play) if provided, or simply the next player 
        private void AdvanceTurn(int index = -1)
        {
            Pot = GetAllPlayers().Select(p => p.BetForGame).Sum();

            var toSet = false;
            var didSet = false;

            foreach (var player in GetAllPlayers())
            {
                if (toSet && !player.IsOut)
                {
                    player.IsMyTurn = true;
                    didSet = true;
                    break;
                }
                else if (player.IsMyTurn)
                {
                    player.IsMyTurn = false;
                    toSet = true;
                }
            }

            if (!didSet && toSet)
            {
                foreach (var player in GetAllPlayers())
                {
                    if (!player.IsOut)
                    {
                        player.IsMyTurn = true;
                        break;
                    }
                }
            }

            // If we are the server, this will push updated state to cilents
            SendUpdatedGameState();
        }

        private void CommitAllBets()
        {
            foreach (var player in GetAllPlayers())
            {
                player.PlaceBet(true);
            }

            this.Pot = GetAllPlayers().Select(p => p.BetForGame).Sum();
            this.PendingPot = 0;
        }

        public void ResetFlop()
        {
            Round++;
            LogGameState();
            _deck = Utilities.Shuffle();

            DealCards();

            var newFlop = new ObservableCollection<Card>();
            // Add first facedown to represent deck
            newFlop.Add(new Card(Suit.Club, 1, false));
            Flop = newFlop;
            this.Pot = 0;
            this.PendingPot = 0;
        }

        public void DealCards()
        {
            // Give first card and reset some fields
            foreach (var player in GetAllPlayers())
            {
                if (player.Points == 0)
                {
                    player.IsOut = true;
                }
                else
                {
                    player.IsOut = false;
                    player.IsShowingHand = false;

                    player.GiveCard(_deck.Pop());
                }
            }

            // Give second card 
            foreach (var player in GetAllPlayers())
            {
                if (!player.IsOut)
                {
                    player.GiveCard(_deck.Pop());
                }
            }
        }

        public void AdvanceFlop()
        {
            LogGameState();

            CommitAllBets();

            var newFlop = Flop;

            if (Flop.Count == 1)
            {
                // Add flop
                newFlop.Add(_deck.Pop());
                newFlop.Add(_deck.Pop());
                newFlop.Add(_deck.Pop());
            }
            else if (Flop.Count <= 5)
            {
                // Add turn or river
                newFlop.Add(_deck.Pop());
            }

            Flop = newFlop;
        }

        private void DealClick(object sender, RoutedEventArgs e)
        {
            if (FirstPlayer.IsDealer)
            {
                if (this.IsServer)
                {
                    AdvanceFlop();
                    SendUpdatedGameState();
                }
                else
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var newMessage = new Message() { Type = MessageType.AdvanceFlop, Value = string.Empty, Uuid = MyUuid };
                        var srl = JsonConvert.SerializeObject(newMessage);

                        Client.Send(srl);
                        Console.WriteLine("Flop");
                    }));
                }
            }
        }

        private void Player6Win(object sender, RoutedEventArgs e)
        {
            if (this.IsServer && !SixthPlayer.IsFake)
            {
                CommitAllBets();
                SixthPlayer.Points += this.Pot;
                PassDealer_Click(null, null);
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var newMessage = new Message() { Type = MessageType.DeclareWinner, Value = SixthPlayer.Uuid.ToString(), Uuid = MyUuid };
                    var srl = JsonConvert.SerializeObject(newMessage);

                    Client.Send(srl);
                    Console.WriteLine("Winner");
                }));
            }
        }

        private void Player5Win(object sender, RoutedEventArgs e)
        {
            if (this.IsServer && !FifthPlayer.IsFake)
            {
                CommitAllBets();
                FifthPlayer.Points += this.Pot;
                PassDealer_Click(null, null);
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var newMessage = new Message() { Type = MessageType.DeclareWinner, Value = FifthPlayer.Uuid.ToString(), Uuid = MyUuid };
                    var srl = JsonConvert.SerializeObject(newMessage);

                    Client.Send(srl);
                    Console.WriteLine("Winner");
                }));
            }
        }

        private void Player4Win(object sender, RoutedEventArgs e)
        {
            if (this.IsServer && !FourthPlayer.IsFake)
            {
                CommitAllBets();
                FourthPlayer.Points += this.Pot;
                PassDealer_Click(null, null);
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var newMessage = new Message() { Type = MessageType.DeclareWinner, Value = FourthPlayer.Uuid.ToString(), Uuid = MyUuid };
                    var srl = JsonConvert.SerializeObject(newMessage);

                    Client.Send(srl);
                    Console.WriteLine("Winner");
                }));
            }
        }

        private void Player3Win(object sender, RoutedEventArgs e)
        {
            if (this.IsServer && !ThirdPlayer.IsFake)
            {
                CommitAllBets();
                ThirdPlayer.Points += this.Pot;
                PassDealer_Click(null, null);
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var newMessage = new Message() { Type = MessageType.DeclareWinner, Value = ThirdPlayer.Uuid.ToString(), Uuid = MyUuid };
                    var srl = JsonConvert.SerializeObject(newMessage);

                    Client.Send(srl);
                    Console.WriteLine("Winner");
                }));
            }
        }

        private void Player2Win(object sender, RoutedEventArgs e)
        {
            if (this.IsServer && !SecondPlayer.IsFake)
            {
                CommitAllBets();
                SecondPlayer.Points += this.Pot;
                PassDealer_Click(null, null);
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var newMessage = new Message() { Type = MessageType.DeclareWinner, Value = SecondPlayer.Uuid.ToString(), Uuid = MyUuid };
                    var srl = JsonConvert.SerializeObject(newMessage);

                    Client.Send(srl);
                    Console.WriteLine("Winner");
                }));
            }
        }

        private void Player1Win(object sender, RoutedEventArgs e)
        {
            if (this.IsServer && !FirstPlayer.IsFake)
            {
                CommitAllBets();
                FirstPlayer.Points += this.Pot;
                PassDealer_Click(null, null);
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var newMessage = new Message() { Type = MessageType.DeclareWinner, Value = FirstPlayer.Uuid.ToString(), Uuid = MyUuid };
                    var srl = JsonConvert.SerializeObject(newMessage);

                    Client.Send(srl);
                    Console.WriteLine("Winner");
                }));
            }
        }

        #endregion

        #region ButtonCallbacks

        private void Bet1_Click(object sender, RoutedEventArgs e)
        {
            if (FirstPlayer.PlacePendingBet(1, this.IsServer))
            {
            }
        }

        private void Bet5_Click(object sender, RoutedEventArgs e)
        {
            if (FirstPlayer.PlacePendingBet(5, this.IsServer))
            {
            }
        }

        private void Bet10_Click(object sender, RoutedEventArgs e)
        {
            if (FirstPlayer.PlacePendingBet(10, this.IsServer))
            {
            }
        }

        private void Bet25_Click(object sender, RoutedEventArgs e)
        {
            if (FirstPlayer.PlacePendingBet(25, this.IsServer))
            {
            }
        }

        private void ResetBet_Click(object sender, RoutedEventArgs e)
        {
            if (FirstPlayer.IsMyTurn)
            {
                if (this.IsServer)
                {
                    FirstPlayer.Points += FirstPlayer.BetForRound;
                }

                FirstPlayer.BetForRound = 0;

                // If we are the server, this will push updated state to cilents
                SendUpdatedGameState();
            }
        }

        private void AllIn_Click(object sender, RoutedEventArgs e)
        {
            if (FirstPlayer.PlacePendingBet(FirstPlayer.Points, this.IsServer))
            {
                PlaceBet_Click(null, null);
            }
        }

        private void Check_Click(object sender, RoutedEventArgs e)
        {
            if (FirstPlayer.PlacePendingBet(this.PendingPot - FirstPlayer.BetForRound, this.IsServer))
            {
                PlaceBet_Click(null, null);
            }
        }

        private void PassDealer_Click(object sender, RoutedEventArgs e)
        {
            ResetHand_Click(null, null);
            var dealerIdx = AdvanceDealer();
            var playerCount = GetAllPlayers().Where(p => !p.IsFake).Count();
            AdvanceTurn((dealerIdx + 1) % playerCount); // WIP
        }

        private void Fold_Click(object sender, RoutedEventArgs e)
        {
            if (FirstPlayer.IsMyTurn)
            {
                if (this.IsServer)
                {
                    FirstPlayer.IsOut = true;
                    AdvanceTurn();
                }
                else
                {
                    // If it's still our turn, the server will tell us so on next update
                    FirstPlayer.IsMyTurn = false;

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var newMessage = new Message() { Type = MessageType.Fold, Value = string.Empty, Uuid = MyUuid };
                        var srl = JsonConvert.SerializeObject(newMessage);

                        Client.Send(srl);
                        Console.WriteLine("Fold");
                    }));
                }
            }
        }

        private void ResetHand_Click(object sender, RoutedEventArgs e)
        {
            foreach (var player in GetAllPlayers())
            {
                player.BetForRound = 0;
                player.BetForGame = 0;
                player.ResetHand();
            }

            ResetFlop();
        }

        private void ShowHand_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsServer)
            {
                FirstPlayer.IsShowingHand = true;
                SendUpdatedGameState();
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var newMessage = new Message() { Type = MessageType.ShowHand, Value = string.Empty, Uuid = MyUuid };
                    var srl = JsonConvert.SerializeObject(newMessage);

                    Client.Send(srl);
                    Console.WriteLine("Show");
                }));
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (Client != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var newMessage = new Message() { Type = MessageType.PollState, Value = PlayerName, Uuid = MyUuid };
                    var srl = JsonConvert.SerializeObject(newMessage);

                    Client.Send(srl);
                    Console.WriteLine("Poll state");
                }));
            }
        }

        private void PlaceBet_Click(object sender, RoutedEventArgs e)
        {
            if (FirstPlayer.CanPlaceBet() && (FirstPlayer.BetForRound >= PendingPot) || FirstPlayer.Points < PendingPot)
            {
                // If we're the server, try to place our bet
                var newPending = FirstPlayer.BetForRound;
                if (this.IsServer)// && FirstPlayer.PlaceBet())
                {
                    if (newPending > PendingPot)
                    {
                        PendingPot = newPending;
                    }

                    AdvanceTurn();

                    //PlayerBet(FirstPlayer.BetForRound.ToString(), MyUuid);
                }
                else
                {
                    // If it's still our turn, the server will tell us so on next update
                    FirstPlayer.IsMyTurn = false;

                    // Else send the pending bet
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var newMessage = new Message() { Type = MessageType.Bet, Value = FirstPlayer.BetForRound.ToString(), Uuid = MyUuid };
                        var srl = JsonConvert.SerializeObject(newMessage);

                        Client.Send(srl);
                        Console.WriteLine("Bet");
                    }));
                }
            }
        }

        private void SkipPlayer_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsServer)
            {
                AdvanceTurn();
            }
        }

        private void ToggleRotation_Click(object sender, RoutedEventArgs e)
        {
            RotationState = (PlayerRotationState)(((int)RotationState + 1) % 3);
            if (_savedSettings != null)
            {
                _savedSettings.Rotation = RotationState;
                Settings.Save(_savedSettings);
            }
        }

        #endregion

    }
}
