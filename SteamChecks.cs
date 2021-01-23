using System;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;
using System.Linq;

using Oxide.Core;
using Oxide.Core.Libraries.Covalence;



namespace Oxide.Plugins
{
    [Info("Steam Checks", "Sapd", "5.0.0")]
    [Description("Kick players depending on information on their Steam profile")]
    public class SteamChecks : CovalencePlugin
    {
        /// <summary>
        /// Set of steamids, which already passed the steamcheck test on joining
        /// </summary>
        /// <remarks>
        /// Resets after a plugin reload
        /// </remarks>
        private HashSet<string> passedList;
        /// <summary>
        /// Set of steamids, which failed the steamcheck test on joining
        /// </summary>
        /// <remarks>
        /// Resets after a plugin reload
        /// </remarks>
        private HashSet<string> failedList;

        /// <summary>
        /// Url to the Steam Web API
        /// </summary>
        private const string apiURL = "https://api.steampowered.com";

        /// <summary>
        /// Oxide permission for a whitelist
        /// </summary>
        private const string skipPermission = "steamchecks.skip";

        /// <summary>
        /// API Key to use for the Web API
        /// </summary>
        /// <remarks>
        /// https://steamcommunity.com/dev/apikey
        /// </remarks>
        private string apiKey;
        /// <summary>
        /// Just log instead of actually kicking users?
        /// </summary>
        private bool logInsteadofKick;
        /// <summary>
        /// This message will be appended to all Kick-messages
        /// </summary>
        private string additionalKickMessage;
        /// <summary>
        /// Cache players, which joined and successfully completed the checks
        /// </summary>
        private bool cachePassedPlayers;
        /// <summary>
        /// Cache players, which joined and failed the checks
        /// </summary>
        private bool cacheDeniedPlayers;

        /// <summary>
        /// Kick when the user has a Steam Community ban
        /// </summary>
        private bool kickCommunityBan;
        /// <summary>
        /// Kick when the user has a Steam Trade ban
        /// </summary>
        private bool kickTradeBan;
        /// <summary>
        /// Kick when the user has a private profile
        /// </summary>
        /// <remarks>
        /// Most checks depend on a public profile
        /// </remarks>
        private bool kickPrivateProfile;
        /// <summary>
        /// Kick user, when his hours are hidden
        /// </summary>
        /// <remarks>
        /// A lot of steam users have their hours hidden
        /// </remarks>
        private bool forceHoursPlayedKick;

        /// <summary>
        /// Maximum amount of VAC bans, the user is allowed to have
        /// </summary>
        private int maxVACBans;
        /// <summary>
        /// Maximum amount of game bans, the user is allowed to have
        /// </summary>
        private int maxGameBans;
        /// <summary>
        /// The minimum steam level, the user must have
        /// </summary>
        private int minSteamLevel;
        /// <summary>
        /// Minimum amount of rust played
        /// </summary>
        private int minRustHoursPlayed;
        /// <summary>
        /// Maximum amount of rust played
        /// </summary>
        private int maxRustHoursPlayed;
        /// <summary>
        /// Minimum amount of Steam games played - except Rust
        /// </summary>
        private int minOtherGamesPlayed;
        /// <summary>
        /// Minimum amount of Steam games played - including Rust
        /// </summary>
        private int minAllGamesHoursPlayed;
        /// <summary>
        /// Minimum amount of Steam games
        /// </summary>
        private int minGameCount;
        /// <summary>
        /// Unix-Time, if the account created by the user is newer/higher than it
        /// he won't be allowed
        /// </summary>
        private long maxAccountCreationTime;

        /// <summary>
        /// Loads default configuration options
        /// </summary>
        protected override void LoadDefaultConfig()
        {
            Config["ApiKey"] = "";
            Config["LogInsteadofKick"] = false;
            Config["AdditionalKickMessage"] = "";
            Config["CachePassedPlayers"] = true;
            Config["CacheDeniedPlayers"] = false;
            Config["Kicking"] = new Dictionary<string, bool>
            {
                ["CommunityBan"] = true,
                ["TradeBan"] = true,
                ["PrivateProfile"] = true,
                ["ForceHoursPlayedKick"] = false,
            };
            Config["Thresholds"] = new Dictionary<string, long>
            {
                ["MaxVACBans"] = 1,
                ["MaxGameBans"] = 1,
                ["MinSteamLevel"] = 2,
                ["MaxAccountCreationTime"] = -1,
                ["MinGameCount"] = 3,
                ["MinRustHoursPlayed"] = -1,
                ["MaxRustHoursPlayed"] = -1,
                ["MinOtherGamesPlayed"] = 2,
                ["MinAllGamesHoursPlayed"] = -1
            };
        }

        /// <summary>
        /// Initializes config options, for every plugin start
        /// </summary>
        private void InitializeConfig()
        {
            apiKey = Config.Get<string>("ApiKey");
            logInsteadofKick = Config.Get<bool>("LogInsteadofKick");
            additionalKickMessage = Config.Get<string>("AdditionalKickMessage");
            cachePassedPlayers = Config.Get<bool>("CachePassedPlayers");
            cacheDeniedPlayers = Config.Get<bool>("CacheDeniedPlayers");

            kickCommunityBan = Config.Get<bool>("Kicking", "CommunityBan");
            kickTradeBan = Config.Get<bool>("Kicking", "TradeBan");
            kickPrivateProfile = Config.Get<bool>("Kicking", "PrivateProfile");
            forceHoursPlayedKick = Config.Get<bool>("Kicking", "ForceHoursPlayedKick");

            maxVACBans = Config.Get<int>("Thresholds", "MaxVACBans");
            maxGameBans = Config.Get<int>("Thresholds", "MaxGameBans");


            minSteamLevel = Config.Get<int>("Thresholds", "MinSteamLevel");

            minRustHoursPlayed = Config.Get<int>("Thresholds", "MinRustHoursPlayed") * 60;
            maxRustHoursPlayed = Config.Get<int>("Thresholds", "MaxRustHoursPlayed") * 60;
            minOtherGamesPlayed = Config.Get<int>("Thresholds", "MinOtherGamesPlayed") * 60;
            minAllGamesHoursPlayed = Config.Get<int>("Thresholds", "MinAllGamesHoursPlayed") * 60;

            minGameCount = Config.Get<int>("Thresholds", "MinGameCount");
            maxAccountCreationTime = Config.Get<long>("Thresholds", "MaxAccountCreationTime");

            if (!kickPrivateProfile)
            {
                if (minRustHoursPlayed > 0 || maxRustHoursPlayed > 0 || minOtherGamesPlayed > 0 || minAllGamesHoursPlayed > 0)
                    LogWarning(Lang("WarningPrivateProfileHours"));

                if (minGameCount > 1)
                    LogWarning(Lang("WarningPrivateProfileGames"));

                if (maxAccountCreationTime > 0)
                    LogWarning(Lang("WarningPrivateProfileCreationTime"));

                if (minSteamLevel > 1)
                    LogWarning(Lang("WarningPrivateProfileSteamLevel"));
            }
        }

        /// <summary>
        /// Load default language messages, for every plugin start
        /// </summary>
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Console"] = "Kicking {0}... ({1})",

                ["ErrorAPIConfig"] = "The API key you supplied in the config is empty.. register one here https://steamcommunity.com/dev/apikey",
                ["WarningPrivateProfileHours"] = "**** WARNING: Private profile-kick is off. However a option to kick for minimim amount of hours is on.",
                ["WarningPrivateProfileGames"] = "**** WARNING: Private profile-kick is off. However the option to kick for minimim amount of games is on (MinGameCount).",
                ["WarningPrivateProfileCreationTime"] = "**** WARNING: Private profile-kick is off. However the option to kick for account age is on (MinAccountCreationTime).",
                ["WarningPrivateProfileSteamLevel"] = "**** WARNING: Private profile-kick is off. However the option to kick for steam level is on (MinSteamLevel).",

                ["ErrorHttp"] = "Error while contacting the SteamAPI. Error: {0}.",
                ["ErrorPrivateProfile"] = "This player has a private profile, therefore SteamChecks cannot check their hours.",

                ["KickCommunityBan"] = "You have a Steam Community ban on record.",
                ["KickVacBan"] = "You have too many VAC bans on record.",
                ["KickGameBan"] = "You have too many Game bans on record.",
                ["KickTradeBan"] = "You have a Steam Trade ban on record.",
                ["KickPrivateProfile"] = "Your Steam profile state is set to private.",
                ["KickMinSteamLevel"] = "Your Steam level is not high enough.",
                ["KickMinRustHoursPlayed"] = "You haven't played enough hours.",
                ["KickMaxRustHoursPlayed"] = "You have played too much Rust.",
                ["KickMinSteamHoursPlayed"] = "You didn't play enough Steam games (hours).",
                ["KickMinNonRustPlayed"] = "You didn't play enough Steam games besides Rust (hours).",
                ["KickHoursPrivate"] = "Your Steam profile is public, but the hours you played is hidden'.",
                ["KickGameCount"] = "You don't have enough Steam games.",
                ["KickMaxAccountCreationTime"] = "Your Steam account is too new.",

                ["KickGeneric"] = "Your Steam account fails our test.",
            }, this);
        }

        /// <summary>
        /// Called by Oxide when plugin starts
        /// </summary>
        private void Init()
        {
            InitializeConfig();

            if (string.IsNullOrEmpty(apiKey))
            {
                LogError(Lang("ErrorAPIConfig"));
                return;
            }

            passedList = new HashSet<string>();
            failedList = new HashSet<string>();

            permission.RegisterPermission(skipPermission, this);
        }

        /// <summary>
        /// Called when a user connects (but before he is spawning)
        /// </summary>
        /// <param name="player"></param>
        private void OnUserConnected(IPlayer player)
        {
            if (string.IsNullOrEmpty(apiKey))
                return;

            if (player.HasPermission(skipPermission))
            {
                Log("{0} / {1} in whitelist (via permission {2})", player.Name, player.Id, skipPermission);
                return;
            }

            // Check temporary White/Blacklist if kicking is enabled
            if (!logInsteadofKick)
            {
                // Player already passed the checks, since the plugin is active
                if (cachePassedPlayers && passedList.Contains(player.Id))
                {
                    Log("{0} / {1} passed all checks already previously", player.Name, player.Id);
                    return;
                }

                // Player already passed the checks, since the plugin is active
                if (cacheDeniedPlayers && failedList.Contains(player.Id))
                {
                    Log("{0} / {1} failed a check already previously", player.Name, player.Id);
                    player.Kick(Lang("KickGeneric") + " " + additionalKickMessage);
                    return;
                }
            }

            CheckPlayer(player.Id, (playerAllowed, reason) =>
            {
                if (playerAllowed)
                {
                    Log("{0} / {1} passed all checks", player.Name, player.Id);
                    passedList.Add(player.Id);
                }
                else
                {
                    if (logInsteadofKick)
                    {
                        Log("{0} / {1} would have been kicked. Reason: {2}", player.Name, player.Id, reason);
                    }
                    else
                    {
                        Log("{0} / {1} kicked. Reason: {2}", player.Name, player.Id, reason);
                        failedList.Add(player.Id);
                        player.Kick(reason + " " + additionalKickMessage);
                    }
                }
            });
        }

        /// <summary>
        /// Checks a steamid, wether it would be allowed into the server
        /// </summary>
        /// <param name="steamid">steamid64 of the user</param>
        /// <param name="callback">
        /// First parameter is true, when the user is allowed, otherwise false
        /// Second parameter is the reason why he is not allowed, filled out when first is false
        /// </param>
        /// <remarks>
        /// Asynchrounously
        /// Runs through all checks one-by-one
        /// 1. Bans
        /// 2. Player Summaries (Private profile, Creation time)
        /// 3. Player Level
        /// Via <see cref="CheckPlayerGameTime"/>
        /// 4. Game Hours and Count
        /// 5. Game badges, to get amount of games if user has hidden Game Hours
        /// </remarks>
        private void CheckPlayer(string steamid, Action<bool, string> callback)
        {
            // Check Bans first, as they are also visible on private profiles
            GetPlayerBans(steamid, (banStatusCode, banResponse) =>
            {
                if (banStatusCode != (int)SteamChecks.StatusCode.Success)
                {
                    APIError(steamid, "GetPlayerBans", banStatusCode);
                    return;
                }

                if (banResponse.CommunityBan && kickCommunityBan)
                {
                    callback(false, Lang("KickCommunityBan"));
                    return;
                }
                if (banResponse.EconomyBan && kickTradeBan)
                {
                    callback(false, Lang("KickTradeBan"));
                    return;
                }
                if (banResponse.GameBanCount > maxGameBans)
                {
                    callback(false, Lang("KickGameBan"));
                    return;
                }
                // The Steam API returns two values, one is the count of VAC bans and one a boolean
                // We will check both to be sure
                if (banResponse.VacBanCount > maxVACBans || (banResponse.VacBan && maxVACBans == 0))
                {
                    callback(false, Lang("KickVacBan"));
                    return;
                }

                // Next, get Player summaries - we have to check if the profile is public
                GetSteamPlayerSummaries(steamid, (sumStatuscode, sumResult) =>
                {
                    if (sumStatuscode != (int)SteamChecks.StatusCode.Success)
                    {
                        APIError(steamid, "GetSteamPlayerSummaries", sumStatuscode);
                        return;
                    }

                    // Is profile not public?
                    if (sumResult.Visibility != PlayerSummary.VisibilityType.Public)
                    {
                        if (kickPrivateProfile)
                        {
                            callback(false, Lang("KickPrivateProfile"));
                            return;
                        }
                        else
                        {
                            // If it is not public, we can cancel checks here and allow the player in
                            callback(true, null);
                            return;
                        }
                    }

                    // Check how old the account is
                    if (maxAccountCreationTime > 0 && sumResult.Timecreated > maxAccountCreationTime)
                    {
                        callback(false, Lang("KickMaxAccountCreationTime"));
                        return;
                    }

                    // Check Steam Level
                    if (minSteamLevel > 1)
                    {
                        GetSteamLevel(steamid, (steamLevelStatusCode, steamLevelResult) =>
                        {
                            if (steamLevelStatusCode != (int)SteamChecks.StatusCode.Success)
                            {
                                APIError(steamid, "GetSteamLevel", sumStatuscode);
                                return;
                            }

                            if (minSteamLevel > steamLevelResult)
                            {
                                callback(false, Lang("KickMinSteamLevel"));
                                return;
                            }
                            else
                            {
                                // Check game time, and amount of games
                                if (minGameCount > 1 || minRustHoursPlayed > 0 || maxRustHoursPlayed > 0 ||
                                        minOtherGamesPlayed > 0 || minAllGamesHoursPlayed > 0)
                                    CheckPlayerGameTime(steamid, callback);
                            }
                        });
                    }
                    // Else, if level check not done, Check game time, and amount of games
                    else if (minGameCount > 1 || minRustHoursPlayed > 0 || maxRustHoursPlayed > 0 ||
                            minOtherGamesPlayed > 0 || minAllGamesHoursPlayed > 0)
                    {
                        CheckPlayerGameTime(steamid, callback);
                    }
                    else // Player now already passed all checks
                    {
                        callback(true, null);
                    }
                });
            });
        }

        /// <summary>
        /// Checks a steamid, wether it would be allowed into the server
        /// Called by <see cref="CheckPlayer"/>
        /// </summary>
        /// <param name="steamid">steamid64 of the user</param>
        /// <param name="callback">
        /// First parameter is true, when the user is allowed, otherwise false
        /// Second parameter is the reason why he is not allowed, filled out when first is false
        /// </param>
        /// <remarks>
        /// Regards those specific parts:
        /// - Game Hours and Count
        /// - Game badges, to get amount of games if user has hidden Game Hours
        /// </remarks>
        void CheckPlayerGameTime(string steamid, Action<bool, string> callback)
        {
            GetPlaytimeInformation(steamid, (gameTimeStatusCode, gameTimeResult) =>
            {
                // Players can additionally hide their play time, check
                bool gametimeHidden = false;
                if (gameTimeStatusCode == (int)SteamChecks.StatusCode.GameInfoHidden)
                {
                    gametimeHidden = true;
                }
                // Check if the request failed in general
                else if (gameTimeStatusCode != (int)SteamChecks.StatusCode.Success)
                {
                    APIError(steamid, "GetPlaytimeInformation", gameTimeStatusCode);
                    return;
                }

                // In rare cases, the SteamAPI returns all games, however with the gametime set to 0. (when the user has this info hidden)
                if (gameTimeResult != null && (gameTimeResult.PlaytimeRust == 0 || gameTimeResult.PlaytimeAll == 0))
                    gametimeHidden = true;

                // If the server owner really wants a hour check, we will kick
                if (gametimeHidden && forceHoursPlayedKick)
                {
                    if (minRustHoursPlayed > 0 || maxRustHoursPlayed > 0 ||
                        minOtherGamesPlayed > 0 || minAllGamesHoursPlayed > 0)
                    {
                        callback(false, Lang("KickHoursPrivate"));
                        return;
                    }
                }
                // Check the times and game count now, when not hidden
                else if (!gametimeHidden)
                {
                    if (minRustHoursPlayed > 0 && gameTimeResult.PlaytimeRust < minRustHoursPlayed)
                    {
                        callback(false, Lang("KickMinRustHoursPlayed"));
                        return;
                    }
                    if (maxRustHoursPlayed > 0 && gameTimeResult.PlaytimeRust > maxRustHoursPlayed)
                    {
                        callback(false, Lang("KickMaxRustHoursPlayed"));
                        return;
                    }
                    if (minAllGamesHoursPlayed > 0 && gameTimeResult.PlaytimeAll < minAllGamesHoursPlayed)
                    {
                        callback(false, Lang("KickMinSteamHoursPlayed"));
                        return;
                    }
                    if (minOtherGamesPlayed > 0 &&
                        (gameTimeResult.PlaytimeAll - gameTimeResult.PlaytimeRust) < minOtherGamesPlayed &&
                        gameTimeResult.GamesCount > 1) // it makes only sense to check, if there are other games in the result set
                    {
                        callback(false, Lang("KickMinNonRustPlayed"));
                        return;
                    }

                    if (minGameCount > 1 && gameTimeResult.GamesCount < minGameCount)
                    {
                        callback(false, Lang("KickGameCount"));
                        return;
                    }
                }

                // If the server owner wants to check minimum amount of games, but the user has hidden game time
                // We will get the count over an additional API request via badges
                if (gametimeHidden && minGameCount > 1)
                {
                    GetSteamBadges(steamid, (badgeStatusCode, badgeResult) =>
                    {
                        // Check if the request failed in general
                        if (badgeStatusCode != (int)SteamChecks.StatusCode.Success)
                        {
                            APIError(steamid, "GetPlaytimeInformation", gameTimeStatusCode);
                            return;
                        }

                        int gamesOwned = ParseBadgeLevel(badgeResult, Badge.GamesOwned);
                        if (gamesOwned < minGameCount)
                        {
                            callback(false, Lang("KickGameCount"));
                            return;
                        }
                        else
                        {
                            // Checks passed
                            callback(true, null);
                            return;
                        }
                    });
                }
                else
                {
                    // Checks passed
                    callback(true, null);
                }
            });
        }

        #region WebAPI

        /// <summary>
        /// HTTP Status Codes (positive) and
        /// custom status codes (negative)
        /// 
        /// 200 is successfull in all cases
        /// </summary>
        enum StatusCode
        {
            Success = 200,
            BadRequest = 400,
            Unauthorized = 401,
            Forbidden = 403,
            NotFound = 404,
            MethodNotAllowed = 405,
            TooManyRequests = 429,
            InternalError = 500,
            Unavailable = 503,

            /// <summary>
            /// User has is games and game hours hidden
            /// </summary>
            GameInfoHidden = -100,
            /// <summary>
            /// Invalid steamid
            /// </summary>
            PlayerNotFound = -101
        }

        /// <summary>
        /// Type of Steam request
        /// </summary>
        enum SteamRequestType
        {
            /// <summary>
            /// Allows to request only one SteamID
            /// </summary>
            IPlayerService,
            /// <summary>
            /// Allows to request multiple SteamID
            /// But only one used
            /// </summary>
            ISteamUser
        }

        /// <summary>
        /// Generic request to the Steam Web API
        /// </summary>
        /// <param name="steamRequestType"></param>
        /// <param name="endpoint">The specific endpoint, e.g. GetSteamLevel/v1</param>
        /// <param name="steamid64"></param>
        /// <param name="callback">Callback returning the HTTP status code <see cref="StatusCode"/> and a JSON JObject</param>
        /// <param name="additionalArguments">Additional arguments, e.g. &foo=bar</param>
        private void SteamWebRequest(SteamRequestType steamRequestType, string endpoint, string steamid64,
            Action<int, JObject> callback, string additionalArguments = "")
        {
            string requestUrl = String.Format("{0}/{1}/{2}/?key={3}&{4}={5}{6}", apiURL, steamRequestType.ToString(),
                endpoint, apiKey, steamRequestType == SteamRequestType.IPlayerService ? "steamid" : "steamids", steamid64, additionalArguments);

            webrequest.Enqueue(requestUrl, "", (httpCode, response) =>
            {
                if (httpCode == (int)StatusCode.Success)
                {
                    callback(httpCode, JObject.Parse(response));
                }
                else
                {
                    callback(httpCode, null);
                }
            }, this);
        }

        /// <summary>
        /// Get the Steam level of a user
        /// </summary>
        /// <param name="steamid64">The users steamid64</param>
        /// <param name="callback">Callback with the statuscode <see cref="StatusCode"/> and the steamlevel</param>
        private void GetSteamLevel(string steamid64, Action<int, int> callback)
        {
            SteamWebRequest(SteamRequestType.IPlayerService, "GetSteamLevel/v1", steamid64,
                (httpCode, jsonResponse) =>
                {
                    if (httpCode == (int)StatusCode.Success)
                        callback(httpCode, (int)jsonResponse["response"]["player_level"]);
                    else
                        callback(httpCode, -1);
                });
        }

        /// <summary>
        /// Struct for the GetOwnedGames API request
        /// </summary>
        class GameTimeInformation
        {
            /// <summary>
            /// Amount of games the user has
            /// </summary>
            public int GamesCount { get; set; }
            /// <summary>
            /// Play time in rust
            /// </summary>
            public int PlaytimeRust { get; set; }
            /// <summary>
            /// Play time accross all Steam games
            /// </summary>
            public int PlaytimeAll { get; set; }

            public GameTimeInformation(int gamesCount, int playtimeRust, int playtimeAll)
            {
                this.GamesCount = gamesCount;
                this.PlaytimeRust = playtimeRust;
                this.PlaytimeAll = playtimeAll;
            }

            public override string ToString()
            {
                return String.Format("Gamescount: {0} - Playtime in Rust: {1} - Playtime all Steam games: {2}", GamesCount, PlaytimeRust, PlaytimeAll);
            }
        }

        /// <summary>
        /// Get information about hours played in Steam
        /// </summary>
        /// <param name="steamid64">steamid64 of the user</param>
        /// <param name="callback">Callback with the statuscode <see cref="StatusCode"/> and the <see cref="GameTimeInformation"/></param>
        /// <remarks>
        /// Even when the user has his profile public, this can be hidden. This seems to be often the case.
        /// When hidden, the statuscode will be <see cref="StatusCode.GameInfoHidden"/>
        /// </remarks>
        private void GetPlaytimeInformation(string steamid64, Action<int, GameTimeInformation> callback)
        {
            SteamWebRequest(SteamRequestType.IPlayerService, "GetOwnedGames/v1", steamid64,
                (httpCode, jsonResponse) =>
                {
                    if (httpCode == (int)StatusCode.Success)
                    {
                        // We need to check wether it is null, because the steam-user can hide game information
                        JToken gamesCountJSON = jsonResponse["response"]?["game_count"];
                        if (gamesCountJSON == null)
                        {
                            callback((int)StatusCode.GameInfoHidden, null);
                            return;
                        }

                        // Also do another null check
                        int gamescount = (int)gamesCountJSON;
                        JToken playtimeRustjson = jsonResponse.SelectToken("$...games[?(@.appid == 252490)].playtime_forever", false);
                        if (playtimeRustjson == null)
                        {
                            callback((int)StatusCode.GameInfoHidden, null);
                            return;
                        }
                        int playtimeRust = (int)playtimeRustjson;
                        int playtimeAll = (int)jsonResponse["response"]["games"].Sum(m => (int)m.SelectToken("playtime_forever"));
                        callback(httpCode, new GameTimeInformation(gamescount, playtimeRust, playtimeAll));
                    }
                    else
                    {
                        callback(httpCode, null);
                    }
                }, "&include_appinfo=false"); // We dont need additional appinfos, like images
        }

        /// <summary>
        /// Struct for the GetPlayerSummaries/v2 Web API request
        /// </summary>
        public class PlayerSummary
        {
            /// <summary>
            /// How visible the Steam Profile is
            /// </summary>
            public enum VisibilityType
            {
                Private = 1,
                Friend = 2,
                Public = 3
            }

            public VisibilityType Visibility { get; set; }
            /// <summary>
            /// URL to his steam profile
            /// </summary>
            public string Profileurl { get; set; }
            /// <summary>
            /// When his account was created - in Unix time
            /// </summary>
            /// <remarks>
            /// Will only be filled, if the users profile is public
            /// </remarks>
            public long Timecreated { get; set; }

            public override string ToString()
            {
                return String.Format("Steam profile visibility: {0} - Profile URL: {1} - Account created: {2}",
                    Visibility.ToString(), Profileurl, Timecreated.ToString());
            }
        }

        /// <summary>
        /// Get Summary information about the player, like if his profile is visible
        /// </summary>
        /// <param name="steamid64">steamid64 of the user</param>
        /// <param name="callback">Callback with the statuscode <see cref="StatusCode"/> and the <see cref="PlayerSummary"/></param>
        private void GetSteamPlayerSummaries(string steamid64, Action<int, PlayerSummary> callback)
        {
            SteamWebRequest(SteamRequestType.ISteamUser, "GetPlayerSummaries/v2", steamid64,
                (httpCode, jsonResponse) =>
                {
                    if (httpCode == (int)StatusCode.Success)
                    {
                        if (jsonResponse["response"]["players"].Count() != 1)
                        {
                            callback((int)StatusCode.PlayerNotFound, null);
                            return;
                        }

                        PlayerSummary summary = new PlayerSummary();
                        summary.Visibility = (PlayerSummary.VisibilityType)(int)jsonResponse["response"]["players"][0]["communityvisibilitystate"];
                        summary.Profileurl = (string)jsonResponse["response"]["players"][0]["profileurl"];

                        // Account creation time can be only fetched, when the profile is public
                        if (summary.Visibility == PlayerSummary.VisibilityType.Public)
                            summary.Timecreated = (int)jsonResponse["response"]["players"][0]["timecreated"];
                        else
                            summary.Timecreated = -1;

                        callback(httpCode, summary);
                    }
                    else
                    {
                        callback(httpCode, null);
                    }
                });
        }

        /// <summary>
        /// The badges we reference.
        /// </summary>
        /// <remarks>
        /// Every badge comes with a level, and EXP gained
        /// </remarks>
        enum Badge
        {
            /// <summary>
            /// Badge for the amount of games owned
            /// </summary>
            /// <remarks>
            /// The level in this badge is exactly to the amount of games owned
            /// E.g. 42 games == level 42 for badge 13
            /// (so not the same as shown on the steam profiles)
            /// </remarks>
            GamesOwned = 13
        }

        /// <summary>
        /// Get all Steam Badges
        /// </summary>
        /// <param name="steamid64">steamid64 of the user</param>
        /// <param name="callback">Callback with the statuscode <see cref="StatusCode"/> and the result as JSON</param>
        private void GetSteamBadges(string steamid64, Action<int, JObject> callback)
        {
            SteamWebRequest(SteamRequestType.IPlayerService, "GetBadges/v1", steamid64,
                (httpCode, jsonResponse) =>
                {
                    callback(httpCode, jsonResponse);
                });
        }

        /// <summary>
        /// Fetched the level of a given badgeid from a JSON Web API result
        /// </summary>
        /// <param name="json">Result JSON as generated by <see cref="GetSteamBadges"/></param>
        /// <param name="badgeID">ID of the badge, see <see cref="Badge"/></param>
        /// <returns>level of the badge, or 0 if badge not existing</returns>
        private int ParseBadgeLevel(JObject json, Badge badgeID)
        {
            JToken token = json.SelectToken("$..badges[?(@.badgeid == " + (int)badgeID + ")].level", false);
            if (token == null)
                return 0;
            else
                return (int)token;
        }

        /// <summary>
        /// Struct for the GetPlayerBans/v1 Web API
        /// </summary>
        public class PlayerBans
        {
            /// <summary>
            /// Wether the user has a community ban
            /// </summary>
            public bool CommunityBan { get; set; }
            /// <summary>
            /// Seems to be true, when the steam user has at least one ban
            /// </summary>
            public bool VacBan { get; set; }
            /// <summary>
            /// Amount of VAC Bans
            /// </summary>
            public int VacBanCount { get; set; }
            /// <summary>
            /// When the last ban was, in Unix time
            /// </summary>
            /// <remarks>
            /// The steam profile only shows bans in the last 7 years
            /// </remarks>
            public int LastBan { get; set; }
            /// <summary>
            /// Amount of game bans
            /// </summary>
            public int GameBanCount { get; set; }
            /// <summary>
            /// If the user is economy banned
            /// </summary>
            public bool EconomyBan { get; set; }

            public override string ToString()
            {
                return String.Format("Community Ban: {0} - VAC Ban: {1} - VAC Ban Count: {2} - Last Ban: {3} - Game Ban Count: {4} - Economy Ban: {5}",
                    CommunityBan, VacBan, VacBanCount, LastBan, GameBanCount, EconomyBan);
            }
        }

        /// <summary>
        /// Get the information about the bans the player has
        /// </summary>
        /// <param name="steamid64">steamid64 of the user</param>
        /// <param name="callback">Callback with the statuscode <see cref="StatusCode"/> and the result as <see cref="PlayerBans"/></param>
        /// <remarks>
        /// Getting the user bans is even possible, if the profile is private
        /// </remarks>
        private void GetPlayerBans(string steamid64, Action<int, PlayerBans> callback)
        {
            SteamWebRequest(SteamRequestType.ISteamUser, "GetPlayerBans/v1", steamid64,
                (httpCode, jsonResponse) =>
                {
                    if (httpCode == (int)StatusCode.Success)
                    {
                        if (jsonResponse["players"].Count() != 1)
                        {
                            callback((int)StatusCode.PlayerNotFound, null);
                            return;
                        }
                        PlayerBans bans = new PlayerBans();
                        bans.CommunityBan = (bool)jsonResponse["players"][0]["CommunityBanned"];
                        bans.VacBan = (bool)jsonResponse["players"][0]["VACBanned"];
                        bans.VacBanCount = (int)jsonResponse["players"][0]["NumberOfVACBans"];
                        bans.LastBan = (int)jsonResponse["players"][0]["DaysSinceLastBan"];
                        bans.GameBanCount = (int)jsonResponse["players"][0]["NumberOfGameBans"];
                        // can be none, probation or banned
                        bans.EconomyBan = (string)jsonResponse["players"][0]["EconomyBan"] == "none" ? false : true;

                        callback(httpCode, bans);
                    }
                    else
                    {
                        callback(httpCode, null);
                    }
                });
        }

        #endregion WebAPI

        #region Utility
        /// <summary>
        /// Abbreviation for printing Language-Strings
        /// </summary>
        /// <param name="key">Languge Key</param>
        /// <param name="userId"></param>
        /// <returns></returns>
        private string Lang(string key, string userId = null) => lang.GetMessage(key, this, userId);

        /// <summary>
        /// Utility function for printing a log when a HTTP API Error was encountered
        /// </summary>
        /// <param name="steamid">steamid64 for which user the request was</param>
        /// <param name="function">functionname in the plugin</param>
        /// <param name="statusCode">see <see cref="StatusCode"/></param>
        private void APIError(string steamid, string function, int statusCode)
        {
            string detailedError = String.Format(" SteamID: {0} - Function: {1} - ErrorCode: {2}", steamid, function, (StatusCode)statusCode);
            LogWarning(Lang("ErrorHttp"), detailedError);
        }
        #endregion Utility

        #region Test

        private const string pluginPrefix = "[SteamChecks] ";
        private void TestResult(IPlayer player, string function, string result)
        {
            player.Reply(pluginPrefix + String.Format("{0} - {1}", function, result));
        }

        /// <summary>
        /// Command, which checks a steamid64 - with the same method when a user joins
        /// </summary>
        /// <param name="player"></param>
        /// <param name="command"></param>
        /// <param name="args">steamid64 to test for</param>
        [Command("steamcheck"), Permission("steamchecks.use")]
        private void SteamCheckCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length != 1)
            {
                TestResult(player, "SteamCheckTests", "You have to provide a SteamID64 as first argument");
                return;
            }

            string steamid = args[0];

            CheckPlayer(steamid, (playerAllowed, reason) =>
            {
                if (playerAllowed)
                    TestResult(player, "CheckPlayer", "The player would pass the checks");
                else
                    TestResult(player, "CheckPlayer", "The player would not pass the checks. Reason: " + reason);
            });
        }

        /// <summary>
        /// Unit tests for all Web API functions
        /// Returns detailed results of the queries.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="command"></param>
        /// <param name="args">steamid64 to test for</param>
        [Command("steamcheck.runtests"), Permission("steamchecks.use")]
        private void SteamCheckTests(IPlayer player, string command, string[] args)
        {
            if (args.Length != 1)
            {
                TestResult(player, "SteamCheckTests", "You have to provide a SteamID64 as first argument");
                return;
            }

            string steamid = args[0];

            GetSteamLevel(steamid, (StatusCode, response) =>
            {
                TestResult(player, "GetSteamLevel", String.Format("Status {0} - Response {1}", ((StatusCode)StatusCode).ToString(), response));
            });

            GetPlaytimeInformation(steamid, (StatusCode, response) =>
            {
                TestResult(player, "GetPlaytimeInformation", String.Format("Status {0} - Response {1}", ((StatusCode)StatusCode).ToString(), response?.ToString()));
            });

            GetSteamPlayerSummaries(steamid, (StatusCode, response) =>
            {
                TestResult(player, "GetSteamPlayerSummaries", String.Format("Status {0} - Response {1}", ((StatusCode)StatusCode).ToString(), response?.ToString()));
            });

            GetSteamBadges(steamid, (StatusCode, response) =>
            {
                if (((StatusCode)StatusCode) == SteamChecks.StatusCode.Success)
                {
                    TestResult(player, "GetSteamBadges", String.Format("Status {0} - Response {1}", ((StatusCode)StatusCode).ToString(), response?.ToString()));

                    int badgeLevel = ParseBadgeLevel(response, Badge.GamesOwned);
                    TestResult(player, "GetSteamBadges - Badge 13, Games owned", String.Format("Status {0} - Response {1}", ((StatusCode)StatusCode).ToString(), badgeLevel));
                }
                else
                {
                    TestResult(player, "GetSteamBadges", String.Format("Status {0}", ((StatusCode)StatusCode).ToString()));
                }
            });

            GetPlayerBans(steamid, (StatusCode, response) =>
            {
                TestResult(player, "GetPlayerBans", String.Format("Status {0} - Response {1}", ((StatusCode)StatusCode).ToString(), response?.ToString()));
            });
        }

        #endregion Test

    }
}
