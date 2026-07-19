using OpenCivOne.Graphics;

namespace OpenCivOne
{
	public class GameData
	{
		// Game group data
		public string GameKey = "";
		public string GameName = "";

		// Game common data
		public short TurnCount = 0;
		public short Year = 0;
		public ushort RandomSeed = 0;
		public short DifficultyLevel = 0;

		public short HumanPlayerID = 0;
		public short PlayerFlags = 0;
		public short PlayerIdentityFlags = 0;
		public short ActiveCivilizations = 0;

		public short PeaceTurnCount = 0;
		public short AIOpponentCount = 0;
		public short NextAnthologyTurn = 80;

		// Game settings
		public GameSettings GameSettingFlags = new GameSettings();
		public short DebugFlags = 0;

		// Game map
		private GBitmap map = new GBitmap(320, 200);

		// Players
		public Player[] Players = new Player[8];

		// Nations: IDs 0 and 8 are fixed barbarian factions; IDs 1-7 and 9-15 are drawn
		// from the configurable pool in server/Data/Nations.json (see NationPool) and
		// reshuffled per game via ShuffleNations(RandomSeed) — see that method's callers
		// for why the seed (not a fresh Random) drives the draw.
		private NationDefinition[] nationTypes = NationPool.BuildNations(0);

		// Continents
		public short[] PerContinentSizeAndPerOceanSize = new short[128];
		public Continent[] Continents = new Continent[64];
		public Continent[] Oceans = new Continent[64];

		// Graphs
		public byte[] ScoreGraphData = new byte[1200];
		public byte[] PeaceGraphData = new byte[1200];

		// Cities
		public City[] Cities = new City[128];
		public string[] CityNames = new string[] {
			"Rome        \0", "Caesarea    \0", "Carthage    \0", "Nicopolis   \0", "Byzantium   \0",
			"Brundisium  \0", "Syracuse    \0", "Antioch     \0", "Palmyra     \0", "Cyrene      \0",
			"Gordion     \0", "Tyrus       \0", "Jerusalem   \0", "Seleucia    \0", "Ravenna     \0",
			"Artaxata    \0", "Babylon     \0", "Sumer       \0", "Uruk        \0", "Nineveh     \0",
			"Ashur       \0", "Ellipi      \0", "Akkad       \0", "Eridu       \0", "Kish        \0",
			"Nippur      \0", "Shuruppak   \0", "Zariqum     \0", "Izibia      \0", "Nimrud      \0",
			"Arbela      \0", "Zamua       \0", "Berlin      \0", "Leipzig     \0", "Hamburg     \0",
			"Bremen      \0", "Frankfurt   \0", "Bonn        \0", "Nuremberg   \0", "Cologne     \0",
			"Hannover    \0", "Munich      \0", "Stuttgart   \0", "Heidelburg  \0", "Salzburg    \0",
			"Konigsberg  \0", "Dortmund    \0", "Brandenburg \0", "Thebes      \0", "Memphis     \0",
			"Oryx        \0", "Heliopolis  \0", "Gaza        \0", "Alexandria  \0", "Byblos      \0",
			"Cairo       \0", "Coptos      \0", "Edfu        \0", "Pithom      \0", "Busiris     \0",
			"Athribis    \0", "Mendes      \0", "Tanis       \0", "Abydos      \0", "Washington  \0",
			"New York    \0", "Boston      \0", "Philadelphia\0", "Atlanta     \0", "Chicago     \0",
			"Buffalo     \0", "St.Louis    \0", "Detroit     \0", "New Orleans \0", "Baltimore   \0",
			"Denver      \0", "Cincinnati  \0", "Dallas      \0", "Los Angeles \0", "Las Vegas   \0",
			"Athens      \0", "Sparta      \0", "Corinth     \0", "Delphi      \0", "Eretria     \0",
			"Pharsalos   \0", "Argos       \0", "Mycenae     \0", "Herakleia   \0", "Antioch     \0",
			"Ephesos     \0", "Rhodes      \0", "Knossos     \0", "Troy        \0", "Pergamon    \0",
			"Miletos     \0", "Delhi       \0", "Bombay      \0", "Madras      \0", "Bangalore   \0",
			"Calcutta    \0", "Lahore      \0", "Karachi     \0", "Kolhapur    \0", "Jaipur      \0",
			"Hyderabad   \0", "Bengal      \0", "Chittagong  \0", "Punjab      \0", "Dacca       \0",
			"Indus       \0", "Ganges      \0", "Moscow      \0", "Leningrad   \0", "Kiev        \0",
			"Minsk       \0", "Smolensk    \0", "Odessa      \0", "Sevastopol  \0", "Tblisi      \0",
			"Sverdlovsk  \0", "Yakutsk     \0", "Vladivostok \0", "Novograd    \0", "Krasnoyarsk \0",
			"Riga        \0", "Rostov      \0", "Astrakhan   \0", "Zimbabwe    \0", "Ulundi      \0",
			"Bapedi      \0", "Hlobane     \0", "Isandhlwana \0", "Intombe     \0", "Mpondo      \0",
			"Ngome       \0", "Swazi       \0", "Tugela      \0", "Umtata      \0", "Umfolozi    \0",
			"Ibabanago   \0", "Isipezi     \0", "Amatikulu   \0", "Zunguin     \0", "Paris       \0",
			"Orleans     \0", "Lyons       \0", "Tours       \0", "Chartres    \0", "Bordeaux    \0",
			"Rouen       \0", "Avignon     \0", "Marseilles  \0", "Grenoble    \0", "Dijon       \0",
			"Amiens      \0", "Cherbourg   \0", "Poitiers    \0", "Toulouse    \0", "Bayonne     \0",
			"Tenochtitlan\0", "Chiauhtia   \0", "Chapultepec \0", "Coatepec    \0", "Ayotzinco   \0",
			"Itzapalapa  \0", "Iztapam     \0", "Mitxcoac    \0", "Tacubaya    \0", "Tecamac     \0",
			"Tepezinco   \0", "Ticoman     \0", "Tlaxcala    \0", "Xaltocan    \0", "Xicalango   \0",
			"Zumpanco    \0", "Peking      \0", "Shanghai    \0", "Canton      \0", "Nanking     \0",
			"Tsingtao    \0", "Hangchow    \0", "Tientsin    \0", "Tatung      \0", "Macao       \0",
			"Anyang      \0", "Shantung    \0", "Chinan      \0", "Kaifeng     \0", "Ningpo      \0",
			"Paoting     \0", "Yangchow    \0", "London      \0", "Coventry    \0", "Birmingham  \0",
			"Dover       \0", "Nottingham  \0", "York        \0", "Liverpool   \0", "Brighton    \0",
			"Oxford      \0", "Reading     \0", "Exeter      \0", "Cambridge   \0", "Hastings    \0",
			"Canterbury  \0", "Banbury     \0", "Newcastle   \0", "Samarkand   \0", "Bokhara     \0",
			"Nishapur    \0", "Karakorum   \0", "Kashgar     \0", "Tabriz      \0", "Aleppo      \0",
			"Kabul       \0", "Ormuz       \0", "Basra       \0", "Khanbalyk   \0", "Khorasan    \0",
			"Shangtu     \0", "Kazan       \0", "Quinsay     \0", "Kerman      \0", "Mecca       \0",
			"Naples      \0", "Sidon       \0", "Tyre        \0", "Tarsus      \0", "Issus       \0",
			"Cunaxa      \0", "Cremona     \0", "Cannae      \0", "Capua       \0", "Turin       \0",
			"Genoa       \0", "Utica       \0", "Crete       \0", "Damascus    \0", "Verona      \0",
			"Salamis     \0", "Lisbon      \0", "Hamburg     \0", "Prague      \0", "Salzburg    \0",
			"Bergen      \0", "Venice      \0", "Milan       \0", "Ghent       \0", "Pisa        \0",
			"Cordoba     \0", "Seville     \0", "Dublin      \0", "Toronto     \0", "Melbourne   \0",
			"Sydney      \0" };
		public GPoint[] CityPositions = new GPoint[256];

		// Wonders
		public short[] WonderCityID = new short[22];

		// Technology
		public short[] TechnologyFirstDiscoveredBy = new short[72];
		public short MaximumTechnologyCount = 0;

		// Map
		public ushort[,] MapVisibility = new ushort[80, 50];
		public short PollutedSquareCount = 0;
		public short PollutionEffectLevel = 0;
		public short GlobalWarmingCount = 0;

		// Replay data
		public short ReplayDataLength = 0;
		public byte[] ReplayData = new byte[4096];

		// Spaceship
		public short SpaceshipFlags = 0;
		public short AISpaceshipSuccessRate = 0;
		private SpaceshipCell[] aSpaceshipCells = new SpaceshipCell[] {
			new SpaceshipCell(11, 5, 1), new SpaceshipCell(11, 4, 2), new SpaceshipCell(11, 6, 1),
			new SpaceshipCell(11, 7, 2), new SpaceshipCell(12, 4, 0), new SpaceshipCell(13, 4, 0),
			new SpaceshipCell(12, 5, 4), new SpaceshipCell(13, 5, 3), new SpaceshipCell(12, 7, 0),
			new SpaceshipCell(13, 7, 0), new SpaceshipCell(12, 6, 4), new SpaceshipCell(13, 6, 3),
			new SpaceshipCell(12, 8, 4), new SpaceshipCell(13, 8, 3), new SpaceshipCell(12, 3, 4),
			new SpaceshipCell(13, 3, 3), new SpaceshipCell(11, 9, 1), new SpaceshipCell(11, 10, 2),
			new SpaceshipCell(12, 10, 0), new SpaceshipCell(13, 10, 0), new SpaceshipCell(12, 9, 4),
			new SpaceshipCell(13, 9, 3), new SpaceshipCell(11, 3, 2), new SpaceshipCell(11, 2, 1),
			new SpaceshipCell(11, 1, 2), new SpaceshipCell(12, 1, 0), new SpaceshipCell(13, 1, 0),
			new SpaceshipCell(12, 2, 4), new SpaceshipCell(13, 2, 3), new SpaceshipCell(12, 11, 4),
			new SpaceshipCell(13, 11, 3), new SpaceshipCell(12, 0, 4), new SpaceshipCell(13, 0, 3),
			new SpaceshipCell(11, 5, 1), new SpaceshipCell(11, 4, 2), new SpaceshipCell(11, 6, 1),
			new SpaceshipCell(11, 7, 2), new SpaceshipCell(9, 6, 6), new SpaceshipCell(9, 4, 7),
			new SpaceshipCell(11, 3, 2), new SpaceshipCell(10, 3, 0), new SpaceshipCell(9, 3, 0),
			new SpaceshipCell(8, 1, 8), new SpaceshipCell(11, 8, 2), new SpaceshipCell(10, 8, 0),
			new SpaceshipCell(9, 8, 0), new SpaceshipCell(8, 8, 0), new SpaceshipCell(7, 8, 0),
			new SpaceshipCell(7, 6, 7), new SpaceshipCell(8, 3, 0), new SpaceshipCell(7, 3, 0),
			new SpaceshipCell(8, 9, 8), new SpaceshipCell(7, 4, 6), new SpaceshipCell(6, 3, 0),
			new SpaceshipCell(5, 3, 0), new SpaceshipCell(5, 4, 7), new SpaceshipCell(6, 8, 0),
			new SpaceshipCell(5, 8, 0), new SpaceshipCell(4, 9, 8), new SpaceshipCell(5, 6, 6),
			new SpaceshipCell(4, 3, 0), new SpaceshipCell(3, 3, 0), new SpaceshipCell(3, 6, 7),
			new SpaceshipCell(4, 8, 0), new SpaceshipCell(3, 8, 0), new SpaceshipCell(4, 1, 8),
			new SpaceshipCell(3, 4, 6), new SpaceshipCell(-1, -1, -1), new SpaceshipCell(0, 0, 0)};

		// Static objects
		public readonly GPoint[] CityOffsets = new GPoint[] {
			new GPoint(0, -1), new GPoint(1, 0), new GPoint(0, 1), 
			new GPoint(-1, 0), new GPoint(1, -1), new GPoint(1, 1), 
			new GPoint(-1, 1), new GPoint(-1, -1), new GPoint(0, -2), 
			new GPoint(2, 0), new GPoint(0, 2), new GPoint(-2, 0),
			new GPoint(-1, -2), new GPoint(1, -2), new GPoint(2, -1), 
			new GPoint(2, 1), new GPoint(1, 2), new GPoint(-1, 2), 
			new GPoint(-2, 1), new GPoint(-2, -1), new GPoint(0, 0) };

		private UnitDefinition[] unitTypes = new UnitDefinition[] {
			new UnitDefinition(UnitTypeEnum.Settler, "Settlers", TechnologyAdvanceEnum.NewFutureTechnology, UnitMovementTypeEnum.Land, 1, 0, 0, 1, 4, 0, 0, UnitRoleTypeEnum.Settler, TechnologyAdvanceEnum.None),	// 0
			new UnitDefinition(UnitTypeEnum.Militia, "Militia", TechnologyAdvanceEnum.Gunpowder, UnitMovementTypeEnum.Land, 1, 0, 1, 1, 1, 0, 0, UnitRoleTypeEnum.Defense, TechnologyAdvanceEnum.None),	// 1
			new UnitDefinition(UnitTypeEnum.Phalanx, "Phalanx", TechnologyAdvanceEnum.Gunpowder, UnitMovementTypeEnum.Land, 1, 0, 1, 2, 2, 0, 0, UnitRoleTypeEnum.Defense, TechnologyAdvanceEnum.BronzeWorking),	// 2
			new UnitDefinition(UnitTypeEnum.Legion, "Legion", TechnologyAdvanceEnum.Conscription, UnitMovementTypeEnum.Land, 1, 0, 3, 1, 2, 0, 0, UnitRoleTypeEnum.LandAttack, TechnologyAdvanceEnum.IronWorking),	// 3
			new UnitDefinition(UnitTypeEnum.Musketeers, "Musketeers", TechnologyAdvanceEnum.Conscription, UnitMovementTypeEnum.Land, 1, 0, 2, 3, 3, 0, 0, UnitRoleTypeEnum.Defense, TechnologyAdvanceEnum.Gunpowder),	// 4
			new UnitDefinition(UnitTypeEnum.Riflemen, "Riflemen", TechnologyAdvanceEnum.NewFutureTechnology, UnitMovementTypeEnum.Land, 1, 0, 3, 5, 3, 0, 0, UnitRoleTypeEnum.Defense, TechnologyAdvanceEnum.Conscription),	// 5
			new UnitDefinition(UnitTypeEnum.Cavalry, "Cavalry", TechnologyAdvanceEnum.Conscription, UnitMovementTypeEnum.Land, 2, 0, 2, 1, 2, 0, 0, UnitRoleTypeEnum.LandAttack, TechnologyAdvanceEnum.HorsebackRiding),	// 6
			new UnitDefinition(UnitTypeEnum.Knights, "Knights", TechnologyAdvanceEnum.Automobile, UnitMovementTypeEnum.Land, 2, 0, 4, 2, 4, 0, 0, UnitRoleTypeEnum.LandAttack, TechnologyAdvanceEnum.Chivalry),	// 7
			new UnitDefinition(UnitTypeEnum.Catapult, "Catapult", TechnologyAdvanceEnum.Metallurgy, UnitMovementTypeEnum.Land, 1, 0, 6, 1, 4, 0, 0, UnitRoleTypeEnum.LandAttack, TechnologyAdvanceEnum.Mathematics),	// 8
			new UnitDefinition(UnitTypeEnum.Cannon, "Cannon", TechnologyAdvanceEnum.Robotics, UnitMovementTypeEnum.Land, 1, 0, 8, 1, 4, 0, 0, UnitRoleTypeEnum.LandAttack, TechnologyAdvanceEnum.Metallurgy),	// 9
			new UnitDefinition(UnitTypeEnum.Chariot, "Chariot", TechnologyAdvanceEnum.Chivalry, UnitMovementTypeEnum.Land, 2, 0, 4, 1, 4, 0, 0, UnitRoleTypeEnum.LandAttack, TechnologyAdvanceEnum.TheWheel),	// 10
			new UnitDefinition(UnitTypeEnum.Armor, "Armor", TechnologyAdvanceEnum.NewFutureTechnology, UnitMovementTypeEnum.Land, 3, 0, 10, 5, 8, 0, 0, UnitRoleTypeEnum.LandAttack, TechnologyAdvanceEnum.Automobile),	// 11
			new UnitDefinition(UnitTypeEnum.MechanicInfantry, "Mech. Inf.", TechnologyAdvanceEnum.NewFutureTechnology, UnitMovementTypeEnum.Land, 3, 0, 6, 6, 5, 0, 0, UnitRoleTypeEnum.Defense, TechnologyAdvanceEnum.LaborUnion),	// 12
			new UnitDefinition(UnitTypeEnum.Artillery, "Artillery", TechnologyAdvanceEnum.NewFutureTechnology, UnitMovementTypeEnum.Land, 2, 0, 12, 2, 6, 0, 0, UnitRoleTypeEnum.LandAttack, TechnologyAdvanceEnum.Robotics),	// 13
			new UnitDefinition(UnitTypeEnum.Fighter, "Fighter", TechnologyAdvanceEnum.NewFutureTechnology, UnitMovementTypeEnum.Air, 10, 1, 4, 2, 6, 2, 0, UnitRoleTypeEnum.AirAttack, TechnologyAdvanceEnum.Flight),	// 14
			new UnitDefinition(UnitTypeEnum.Bomber, "Bomber", TechnologyAdvanceEnum.NewFutureTechnology, UnitMovementTypeEnum.Air, 8, 2, 12, 1, 12, 2, 0, UnitRoleTypeEnum.LandAttack, TechnologyAdvanceEnum.AdvancedFlight),	// 15
			new UnitDefinition(UnitTypeEnum.Trireme, "Trireme", TechnologyAdvanceEnum.Navigation, UnitMovementTypeEnum.Water, 3, 0, 1, 0, 4, 0, 2, UnitRoleTypeEnum.SeaTransport, TechnologyAdvanceEnum.Mapmaking),	// 16
			new UnitDefinition(UnitTypeEnum.Sail, "Sail", TechnologyAdvanceEnum.Magnetism, UnitMovementTypeEnum.Water, 3, 0, 1, 1, 4, 0, 3, UnitRoleTypeEnum.SeaTransport, TechnologyAdvanceEnum.Navigation),	// 17
			new UnitDefinition(UnitTypeEnum.Frigate, "Frigate", TechnologyAdvanceEnum.Industrialization, UnitMovementTypeEnum.Water, 3, 0, 2, 2, 4, 0, 4, UnitRoleTypeEnum.SeaTransport, TechnologyAdvanceEnum.Magnetism),	// 18
			new UnitDefinition(UnitTypeEnum.Ironclad, "Ironclad", TechnologyAdvanceEnum.Combustion, UnitMovementTypeEnum.Water, 4, 0, 4, 4, 6, 0, 0, UnitRoleTypeEnum.SeaAttack, TechnologyAdvanceEnum.SteamEngine),	// 19
			new UnitDefinition(UnitTypeEnum.Cruiser, "Cruiser", TechnologyAdvanceEnum.NewFutureTechnology, UnitMovementTypeEnum.Water, 6, 0, 6, 6, 8, 3, 0, UnitRoleTypeEnum.SeaAttack, TechnologyAdvanceEnum.Combustion),	// 20
			new UnitDefinition(UnitTypeEnum.Battleship, "Battleship", TechnologyAdvanceEnum.NewFutureTechnology, UnitMovementTypeEnum.Water, 4, 0, 18, 12, 16, 3, 0, UnitRoleTypeEnum.SeaAttack, TechnologyAdvanceEnum.Steel),	// 21
			new UnitDefinition(UnitTypeEnum.Submarine, "Submarine", TechnologyAdvanceEnum.NewFutureTechnology, UnitMovementTypeEnum.Water, 3, 0, 8, 2, 5, 3, 0, UnitRoleTypeEnum.SeaAttack, TechnologyAdvanceEnum.MassProduction),	// 22
			new UnitDefinition(UnitTypeEnum.Carrier, "Carrier", TechnologyAdvanceEnum.NewFutureTechnology, UnitMovementTypeEnum.Water, 5, 0, 1, 12, 16, 3, 0, UnitRoleTypeEnum.SeaAttack, TechnologyAdvanceEnum.AdvancedFlight),	// 23
			new UnitDefinition(UnitTypeEnum.Transport, "Transport", TechnologyAdvanceEnum.NewFutureTechnology, UnitMovementTypeEnum.Water, 4, 0, 0, 3, 5, 0, 8, UnitRoleTypeEnum.SeaTransport, TechnologyAdvanceEnum.Industrialization),	// 24
			new UnitDefinition(UnitTypeEnum.Nuclear, "Nuclear", TechnologyAdvanceEnum.NewFutureTechnology, UnitMovementTypeEnum.Air, 16, 1, 99, 0, 16, 0, 0, UnitRoleTypeEnum.LandAttack, TechnologyAdvanceEnum.Rocketry),	// 25
			new UnitDefinition(UnitTypeEnum.Diplomat, "Diplomat", TechnologyAdvanceEnum.NewFutureTechnology, UnitMovementTypeEnum.Land, 2, 0, 0, 0, 3, 0, 0, UnitRoleTypeEnum.Neutral, TechnologyAdvanceEnum.Writing),	// 26
			new UnitDefinition(UnitTypeEnum.Caravan, "Caravan", TechnologyAdvanceEnum.NewFutureTechnology, UnitMovementTypeEnum.Land, 1, 0, 0, 1, 5, 0, 0, UnitRoleTypeEnum.Neutral, TechnologyAdvanceEnum.Trade)}; // 27

		private ImprovementDefinition[] improvementTypes = new ImprovementDefinition[] {
			new ImprovementDefinition(0, "NONE", 8, 0, TechnologyAdvanceEnum.None, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(1, "Palace", 20, 5, TechnologyAdvanceEnum.Masonry, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(2, "Barracks", 4, 0, TechnologyAdvanceEnum.None, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(3, "Granary", 6, 1, TechnologyAdvanceEnum.Pottery, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(4, "Temple", 4, 1, TechnologyAdvanceEnum.CeremonialBurial, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(5, "MarketPlace", 8, 1, TechnologyAdvanceEnum.Currency, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(6, "Library", 8, 1, TechnologyAdvanceEnum.Writing, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(7, "Courthouse", 8, 1, TechnologyAdvanceEnum.CodeOfLaws, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(8, "City Walls", 12, 2, TechnologyAdvanceEnum.Masonry, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(9, "Aqueduct", 12, 2, TechnologyAdvanceEnum.Construction, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(10, "Bank", 12, 3, TechnologyAdvanceEnum.Banking, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(11, "Cathedral", 16, 3, TechnologyAdvanceEnum.Religion, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(12, "University", 16, 3, TechnologyAdvanceEnum.University, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(13, "Mass Transit", 16, 4, TechnologyAdvanceEnum.MassProduction, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(14, "Colosseum", 10, 4, TechnologyAdvanceEnum.Construction, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(15, "Factory", 20, 4, TechnologyAdvanceEnum.Industrialization, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(16, "Mfg. Plant", 32, 6, TechnologyAdvanceEnum.Robotics, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(17, "SDI Defense", 20, 4, TechnologyAdvanceEnum.Superconductor, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(18, "Recycling Cntr.", 20, 2, TechnologyAdvanceEnum.Recycling, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(19, "Power Plant", 16, 4, TechnologyAdvanceEnum.Refining, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(20, "Hydro Plant", 24, 4, TechnologyAdvanceEnum.Electronics, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(21, "Nuclear Plant", 16, 2, TechnologyAdvanceEnum.NuclearPower, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(22, "SS Structural", 8, 0, TechnologyAdvanceEnum.SpaceFlight, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(23, "SS Component", 16, 0, TechnologyAdvanceEnum.Plastics, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(24, "SS Module", 32, 0, TechnologyAdvanceEnum.Robotics, TechnologyAdvanceEnum.None) };

		private TechnologyAdvanceDefinition[] technologyAdvanceTypes = new TechnologyAdvanceDefinition[] {
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Alphabet, "Alphabet", TechnologyAdvanceEnum.None, TechnologyAdvanceEnum.None),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.CodeOfLaws, "Code of Laws", TechnologyAdvanceEnum.Alphabet, TechnologyAdvanceEnum.None),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Currency, "Currency", TechnologyAdvanceEnum.BronzeWorking, TechnologyAdvanceEnum.None),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.AtomicTheory, "Atomic Theory", TechnologyAdvanceEnum.TheoryOfGravity, TechnologyAdvanceEnum.Physics),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Democracy, "Democracy", TechnologyAdvanceEnum.Philosophy, TechnologyAdvanceEnum.Literacy),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Monarchy, "Monarchy", TechnologyAdvanceEnum.CeremonialBurial, TechnologyAdvanceEnum.CodeOfLaws),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Astronomy, "Astronomy", TechnologyAdvanceEnum.Mysticism, TechnologyAdvanceEnum.Mathematics),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Mapmaking, "MapMaking", TechnologyAdvanceEnum.Alphabet, TechnologyAdvanceEnum.None),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Navigation, "Navigation", TechnologyAdvanceEnum.Mapmaking, TechnologyAdvanceEnum.Astronomy),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Mathematics, "Mathematics", TechnologyAdvanceEnum.Alphabet, TechnologyAdvanceEnum.Masonry),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Medicine, "Medicine", TechnologyAdvanceEnum.Philosophy, TechnologyAdvanceEnum.Trade),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Physics, "Physics", TechnologyAdvanceEnum.Mathematics, TechnologyAdvanceEnum.Navigation),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Engineering, "Engineering", TechnologyAdvanceEnum.TheWheel, TechnologyAdvanceEnum.Construction),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.University, "University", TechnologyAdvanceEnum.Mathematics, TechnologyAdvanceEnum.Philosophy),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Magnetism, "Magnetism", TechnologyAdvanceEnum.Navigation, TechnologyAdvanceEnum.Physics),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Electronics, "Electronics", TechnologyAdvanceEnum.Engineering, TechnologyAdvanceEnum.Electricity),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Masonry, "Masonry", TechnologyAdvanceEnum.None, TechnologyAdvanceEnum.None),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.BronzeWorking, "Bronze Working", TechnologyAdvanceEnum.None, TechnologyAdvanceEnum.None),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.IronWorking, "Iron Working", TechnologyAdvanceEnum.BronzeWorking, TechnologyAdvanceEnum.None),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.BridgeBuilding, "Bridge Building", TechnologyAdvanceEnum.IronWorking, TechnologyAdvanceEnum.Construction),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Invention, "Invention", TechnologyAdvanceEnum.Engineering, TechnologyAdvanceEnum.Literacy),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Computers, "Computers", TechnologyAdvanceEnum.Mathematics, TechnologyAdvanceEnum.Electronics),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Writing, "Writing", TechnologyAdvanceEnum.Alphabet, TechnologyAdvanceEnum.None),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.SteamEngine, "Steam Engine", TechnologyAdvanceEnum.Physics, TechnologyAdvanceEnum.Invention),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Trade, "Trade", TechnologyAdvanceEnum.Currency, TechnologyAdvanceEnum.CodeOfLaws),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.CeremonialBurial, "Ceremonial Burial", TechnologyAdvanceEnum.None, TechnologyAdvanceEnum.None),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Mysticism, "Mysticism", TechnologyAdvanceEnum.CeremonialBurial, TechnologyAdvanceEnum.None),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.NuclearFission, "Nuclear Fission", TechnologyAdvanceEnum.MassProduction, TechnologyAdvanceEnum.AtomicTheory),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Philosophy, "Philosophy", TechnologyAdvanceEnum.Mysticism, TechnologyAdvanceEnum.Literacy),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Religion, "Religion", TechnologyAdvanceEnum.Philosophy, TechnologyAdvanceEnum.Writing),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Literacy, "Literacy", TechnologyAdvanceEnum.Writing, TechnologyAdvanceEnum.CodeOfLaws),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.HorsebackRiding, "Horseback Riding", TechnologyAdvanceEnum.None, TechnologyAdvanceEnum.None),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Feudalism, "Feudalism", TechnologyAdvanceEnum.Masonry, TechnologyAdvanceEnum.Monarchy),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.TheWheel, "The Wheel", TechnologyAdvanceEnum.None, TechnologyAdvanceEnum.None),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Gunpowder, "Gunpowder", TechnologyAdvanceEnum.Invention, TechnologyAdvanceEnum.IronWorking),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Industrialization, "Industrialization", TechnologyAdvanceEnum.Railroad, TechnologyAdvanceEnum.Banking),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Chemistry, "Chemistry", TechnologyAdvanceEnum.University, TechnologyAdvanceEnum.Medicine),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Combustion, "Combustion", TechnologyAdvanceEnum.Refining, TechnologyAdvanceEnum.Explosives),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Flight, "Flight", TechnologyAdvanceEnum.Combustion, TechnologyAdvanceEnum.Physics),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.AdvancedFlight, "Advanced Flight", TechnologyAdvanceEnum.Flight, TechnologyAdvanceEnum.Electricity),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.SpaceFlight, "Space Flight", TechnologyAdvanceEnum.Computers, TechnologyAdvanceEnum.Rocketry),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.MassProduction, "Mass Production", TechnologyAdvanceEnum.Automobile, TechnologyAdvanceEnum.TheCorporation),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Pottery, "Pottery", TechnologyAdvanceEnum.None, TechnologyAdvanceEnum.None),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Communism, "Communism", TechnologyAdvanceEnum.Philosophy, TechnologyAdvanceEnum.Industrialization),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.TheRepublic, "The Republic", TechnologyAdvanceEnum.CodeOfLaws, TechnologyAdvanceEnum.Literacy),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Construction, "Construction", TechnologyAdvanceEnum.Masonry, TechnologyAdvanceEnum.Currency),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Rocketry, "Rocketry", TechnologyAdvanceEnum.AdvancedFlight, TechnologyAdvanceEnum.Electronics),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.TheCorporation, "The Corporation", TechnologyAdvanceEnum.Banking, TechnologyAdvanceEnum.Industrialization),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Metallurgy, "Metallurgy", TechnologyAdvanceEnum.Gunpowder, TechnologyAdvanceEnum.University),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Railroad, "RailRoad", TechnologyAdvanceEnum.SteamEngine, TechnologyAdvanceEnum.BridgeBuilding),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.NuclearPower, "Nuclear Power", TechnologyAdvanceEnum.NuclearFission, TechnologyAdvanceEnum.Electronics),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.TheoryOfGravity, "Theory of Gravity", TechnologyAdvanceEnum.Astronomy, TechnologyAdvanceEnum.University),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Steel, "Steel", TechnologyAdvanceEnum.Metallurgy, TechnologyAdvanceEnum.Industrialization),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Banking, "Banking", TechnologyAdvanceEnum.Trade, TechnologyAdvanceEnum.TheRepublic),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Electricity, "Electricity", TechnologyAdvanceEnum.Metallurgy, TechnologyAdvanceEnum.Magnetism),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Refining, "Refining", TechnologyAdvanceEnum.Chemistry, TechnologyAdvanceEnum.TheCorporation),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Explosives, "Explosives", TechnologyAdvanceEnum.Gunpowder, TechnologyAdvanceEnum.Chemistry),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Superconductor, "SuperConductor", TechnologyAdvanceEnum.Plastics, TechnologyAdvanceEnum.MassProduction),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Automobile, "Automobile", TechnologyAdvanceEnum.Combustion, TechnologyAdvanceEnum.Steel),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.GeneticEngineering, "Genetic Engineering", TechnologyAdvanceEnum.Medicine, TechnologyAdvanceEnum.TheCorporation),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Plastics, "Plastics", TechnologyAdvanceEnum.Refining, TechnologyAdvanceEnum.SpaceFlight),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Recycling, "Recycling", TechnologyAdvanceEnum.MassProduction, TechnologyAdvanceEnum.Democracy),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Chivalry, "Chivalry", TechnologyAdvanceEnum.Feudalism, TechnologyAdvanceEnum.HorsebackRiding),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Robotics, "Robotics", TechnologyAdvanceEnum.Plastics, TechnologyAdvanceEnum.Computers),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.Conscription, "Conscription", TechnologyAdvanceEnum.TheRepublic, TechnologyAdvanceEnum.Explosives),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.LaborUnion, "Labor Union", TechnologyAdvanceEnum.MassProduction, TechnologyAdvanceEnum.Communism),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.FusionPower, "Fusion Power", TechnologyAdvanceEnum.NuclearPower, TechnologyAdvanceEnum.Superconductor),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.FutureTechnology1, "1", TechnologyAdvanceEnum.NewFutureTechnology, TechnologyAdvanceEnum.NewFutureTechnology),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.FutureTechnology2, "2", TechnologyAdvanceEnum.NewFutureTechnology, TechnologyAdvanceEnum.NewFutureTechnology),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.FutureTechnology3, "3", TechnologyAdvanceEnum.NewFutureTechnology, TechnologyAdvanceEnum.NewFutureTechnology),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.FutureTechnology4, "4", TechnologyAdvanceEnum.NewFutureTechnology, TechnologyAdvanceEnum.NewFutureTechnology),
			new TechnologyAdvanceDefinition(TechnologyAdvanceEnum.FutureTechnology5, "Future Tech.", TechnologyAdvanceEnum.NewFutureTechnology, TechnologyAdvanceEnum.NewFutureTechnology)};

		private TerrainModification[] terrainModifications = new TerrainModification[] {
			new TerrainModification(TerrainTypeEnum.Desert, -2, 5, -2, 5, false, false),
			new TerrainModification(TerrainTypeEnum.Plains, -2, 5, 2, 15, true, true),
			new TerrainModification(TerrainTypeEnum.Grassland, -2, 5, 2, 10, false, true),
			new TerrainModification(TerrainTypeEnum.Forest, 6, 5, -1, 5, false, false),
			new TerrainModification(TerrainTypeEnum.Hills, -2, 10, -4, 10, true, true),
			new TerrainModification(TerrainTypeEnum.Mountains, -1, 0, -2, 10, false, false),
			new TerrainModification(TerrainTypeEnum.Tundra, -1, 0, -1, 0, false, false),
			new TerrainModification(TerrainTypeEnum.Arctic, -1, 0, -1, 0, false, false),
			new TerrainModification(TerrainTypeEnum.Swamp, 10, 15, 2, 15, false, false),
			new TerrainModification(TerrainTypeEnum.Jungle, 10, 15, 2, 15, false, false),
			new TerrainModification(TerrainTypeEnum.Water, -1, 0, -1, 0, false, false),
			new TerrainModification(TerrainTypeEnum.River, -2, 5, -1, 0, false, true)};

		private TerrainDefinition[] terrainTypes = new TerrainDefinition[]{
			new TerrainDefinition(TerrainTypeEnum.Desert, "Desert", 1, 2, 0, 1, 0, 1, 14),
			new TerrainDefinition(TerrainTypeEnum.Plains, "Plains", 1, 2, 1, 1, 0, 1, 6),
			new TerrainDefinition(TerrainTypeEnum.Grassland, "Grassland", 1, 2, 2, 1, 0, 1, 10),
			new TerrainDefinition(TerrainTypeEnum.Forest, "Forest", 2, 3, 1, 2, 0, 2, 2),
			new TerrainDefinition(TerrainTypeEnum.Hills, "Hills", 2, 4, 1, 0, 0, 2, 12),
			new TerrainDefinition(TerrainTypeEnum.Mountains, "Mountains", 3, 6, 0, 1, 0, 3, 13),
			new TerrainDefinition(TerrainTypeEnum.Tundra, "Tundra", 1, 2, 1, 0, 0, 0, 7),
			new TerrainDefinition(TerrainTypeEnum.Arctic, "Arctic", 2, 2, 0, 0, 0, 0, 15),
			new TerrainDefinition(TerrainTypeEnum.Swamp, "Swamp", 2, 3, 1, 0, 0, 0, 3),
			new TerrainDefinition(TerrainTypeEnum.Jungle, "Jungle", 2, 3, 1, 0, 0, 0, 11),
			new TerrainDefinition(TerrainTypeEnum.Water, "Ocean", 1, 2, 1, 0, 2, 0, 1),
			new TerrainDefinition(TerrainTypeEnum.River, "River", 1, 3, 2, 1, 1, 2, 9),
			// Terrain with Special Resources
			new TerrainDefinition(TerrainTypeEnum.ResourceOasis, "Oasis", 1, 2, 3, 1, 0, 1, 14),
			new TerrainDefinition(TerrainTypeEnum.ResourceHorses, "Horses", 1, 2, 1, 3, 0, 1, 6),
			new TerrainDefinition(TerrainTypeEnum.ResourceGrassland, "Grassland", 1, 2, 2, 1, 0, 1, 10),
			new TerrainDefinition(TerrainTypeEnum.ResourceGame, "Game", 2, 3, 3, 2, 0, 2, 2),
			new TerrainDefinition(TerrainTypeEnum.ResourceCoal, "Coal", 2, 4, 1, 2, 0, 2, 12),
			new TerrainDefinition(TerrainTypeEnum.ResourceGold, "Gold", 3, 6, 0, 1, 6, 3, 13),
			new TerrainDefinition(TerrainTypeEnum.ResourceGame2, "Game", 1, 2, 3, 0, 0, 0, 7),
			new TerrainDefinition(TerrainTypeEnum.ResourceSeals, "Seals", 2, 2, 2, 0, 0, 0, 15),
			new TerrainDefinition(TerrainTypeEnum.ResourceOil, "Oil", 2, 3, 1, 4, 0, 0, 3),
			new TerrainDefinition(TerrainTypeEnum.ResourceGems, "Gems", 2, 3, 1, 0, 4, 0, 11),
			new TerrainDefinition(TerrainTypeEnum.ResourceFish, "Fish", 1, 2, 3, 0, 2, 0, 1),
			new TerrainDefinition(TerrainTypeEnum.ResourceRiver, "River", 1, 3, 2, 1, 1, 2, 9)};

		private ImprovementDefinition[] wonderTypes = new ImprovementDefinition[] {
			new ImprovementDefinition(0, "NONE", 8, 0, TechnologyAdvanceEnum.None, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(1, "Pyramids", 30, 0, TechnologyAdvanceEnum.Masonry, TechnologyAdvanceEnum.Communism),
			new ImprovementDefinition(2, "Hanging Gardens", 30, 0, TechnologyAdvanceEnum.Pottery, TechnologyAdvanceEnum.Invention),
			new ImprovementDefinition(3, "Colossus", 20, 0, TechnologyAdvanceEnum.BronzeWorking, TechnologyAdvanceEnum.Electricity),
			new ImprovementDefinition(4, "Lighthouse", 20, 0, TechnologyAdvanceEnum.Mapmaking, TechnologyAdvanceEnum.Magnetism),
			new ImprovementDefinition(5, "Great Library", 30, 0, TechnologyAdvanceEnum.Literacy, TechnologyAdvanceEnum.University),
			new ImprovementDefinition(6, "Oracle", 30, 0, TechnologyAdvanceEnum.Mysticism, TechnologyAdvanceEnum.Religion),
			new ImprovementDefinition(7, "Great Wall", 30, 0, TechnologyAdvanceEnum.Masonry, TechnologyAdvanceEnum.Gunpowder),
			new ImprovementDefinition(8, "Magellan's Expedition", 40, 0, TechnologyAdvanceEnum.Navigation, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(9, "Michelangelo's Chapel", 30, 0, TechnologyAdvanceEnum.Religion, TechnologyAdvanceEnum.Communism),
			new ImprovementDefinition(10, "Copernicus' Observatory", 30, 0, TechnologyAdvanceEnum.Astronomy, TechnologyAdvanceEnum.Automobile), // !!! By manual it should obsolete after the development of Automobile
			new ImprovementDefinition(11, "Shakespeare's Theatre", 40, 0, TechnologyAdvanceEnum.Medicine, TechnologyAdvanceEnum.Electronics),
			new ImprovementDefinition(12, "Isaac Newton's College", 40, 0, TechnologyAdvanceEnum.TheoryOfGravity, TechnologyAdvanceEnum.NuclearFission),
			new ImprovementDefinition(13, "J.S.Bach's Cathedral", 40, 0, TechnologyAdvanceEnum.Religion, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(14, "Darwin's Voyage", 30, 0, TechnologyAdvanceEnum.Railroad, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(15, "Hoover Dam", 60, 0, TechnologyAdvanceEnum.Electronics, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(16, "Women's Suffrage", 60, 0, TechnologyAdvanceEnum.Industrialization, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(17, "Manhattan Project", 60, 0, TechnologyAdvanceEnum.NuclearFission, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(18, "United Nations", 60, 0, TechnologyAdvanceEnum.Communism, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(19, "Apollo Program", 60, 0, TechnologyAdvanceEnum.SpaceFlight, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(20, "SETI Program", 60, 0, TechnologyAdvanceEnum.Computers, TechnologyAdvanceEnum.None),
			new ImprovementDefinition(21, "Cure for Cancer", 60, 0, TechnologyAdvanceEnum.GeneticEngineering, TechnologyAdvanceEnum.None) };

		public GameData()
		{
			for (int i = 0; i < this.Players.Length; i++)
			{
				this.Players[i] = new Player();
			}

			for (int i = 0; i < this.PerContinentSizeAndPerOceanSize.Length; i++)
			{
				this.PerContinentSizeAndPerOceanSize[i] = 0;
			}

			for (int i = 0; i < this.Continents.Length; i++)
			{
				this.Continents[i] = new Continent();
			}

			for (int i = 0; i < this.Oceans.Length; i++)
			{
				this.Oceans[i] = new Continent();
			}

			for (int i = 0; i < this.ScoreGraphData.Length; i++)
			{
				this.ScoreGraphData[i] = 0;
			}

			for (int i = 0; i < this.PeaceGraphData.Length; i++)
			{
				this.PeaceGraphData[i] = 0;
			}

			for (int i = 0; i < this.Cities.Length; i++)
			{
				this.Cities[i] = new City(i);
			}

			for (int i = 0; i < this.CityPositions.Length; i++)
			{
				this.CityPositions[i] = new GPoint(0, 0);
			}

			for (int i = 0; i < this.WonderCityID.Length; i++)
			{
				this.WonderCityID[i] = 0;
			}

			for (int i = 0; i < this.TechnologyFirstDiscoveredBy.Length; i++)
			{
				this.TechnologyFirstDiscoveredBy[i] = 0;
			}

			for (int i = 0; i < 80; i++)
			{
				for (int j = 0; j < 50; j++)
					this.MapVisibility[i, j] = 0;
			}

			for (int i = 0; i < this.ReplayData.Length; i++)
			{
				this.ReplayData[i] = 0;
			}
		}

		public GBitmap Map
		{
			get => this.map;
			set
			{
				value.Visible = this.map.Visible;
				this.map = value;
			}
		}

		public SpaceshipCell[] SpaceshipCells
		{
			get => this.aSpaceshipCells;
		}

		public NationDefinition[] Nations
		{
			get => this.nationTypes;
		}

		// Redraws nation IDs 1-7 and 9-15 from the Nations.json pool. Must be called
		// after RandomSeed is set for a new game (GameTools.F0_2dc4_0042_Randomize) and
		// after it's restored from a save file (GameLoadAndSave), so the draw is
		// reproducible: save files only persist RandomSeed and each player's
		// NationalityID, not the nation names/cities themselves.
		public void ShuffleNations(int seed)
		{
			this.nationTypes = NationPool.BuildNations(seed);
			RebuildCityNames();
		}

		// CityNames is a separate flat array (256 = 16 nations x 16 names) that new-city
		// naming reads by a NationalityID-derived block offset (Overlay_20.cs
		// F20_0000_0000, ~line 57: blockIndex = NationalityID - 1, minus 1 more if
		// NationalityID >= 8, then *16). That formula only ever produces 0-13 for real
		// civs (IDs 1-7 -> 0-6, IDs 9-15 -> 7-13) and never touches IDs 0 or 8 -- which is
		// why the original hardcoded array packs the 14 real civs' blocks first,
		// consecutively, then appends Barbarian (ID 0) and the ID-8 placeholder at the
		// end (blocks 14-15), instead of following raw nation-ID order. RebuildCityNames
		// must match that exact layout, not nationTypes[0..15] order, or the blockIndex
		// formula lands on the wrong nation's block.
		private void RebuildCityNames()
		{
			var names = new List<string>(256);
			void AddBlock(NationDefinition nation)
			{
				foreach (var city in nation.Cities)
					names.Add(city.PadRight(12) + "\0");
			}

			for (int id = 1; id <= 7; id++) AddBlock(this.nationTypes[id]);
			for (int id = 9; id <= 15; id++) AddBlock(this.nationTypes[id]);
			AddBlock(this.nationTypes[0]);
			AddBlock(this.nationTypes[8]);

			this.CityNames = names.ToArray();
		}

		public TerrainDefinition[] Terrains
		{
			get => this.terrainTypes;
		}

		public TerrainModification[] TerrainModifications
		{
			get => this.terrainModifications;
		}

		public UnitDefinition[] Units
		{
			get => this.unitTypes;
		}

		/// <summary>
		/// Returns the Improvement or Wonder, depending on the index (0 - 24 are city improvements, 25 - are world Wonders)
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public ImprovementDefinition GetImprovementType(int index)
		{
			if (index < 25)
			{
				return this.improvementTypes[index];
			}
			else
			{
				return this.wonderTypes[index - 24];
			}
		}

		public ImprovementDefinition[] Wonders
		{
			get => this.wonderTypes;
		}

		public TechnologyAdvanceDefinition[] TechnologyAdvances
		{
			get => this.technologyAdvanceTypes;
		}
	}
}
