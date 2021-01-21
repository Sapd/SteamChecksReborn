# SteamChecksReborn

Checks connecting users for configurable criteria of the Steam WebAPI and kicks if not fullfilled.
For uMod/Oxide (e.g. the Rust game).

Complete rewrite of the original SteamChecks plugin.  

## Permissions

* `steamchecksreborn.use`  -- Allows to issue test commands
* `steamchecksreborn.skip` -- Users with this permission, won't be checked on connect

## Server/Console Commands

* `steamcheck <steamid64>` -- Checks the given steamid64 (does not matter wether connected), for all configured criteria and returns wether he would have been kicked
* `steamcheck.runtests <steamid64>` -- Calls all WebAPI functions with the given steamid64 and returns detailed output. Use this output when creating an issue.

## Configuration

Default configuration:
```json
{
  "ApiKey": "",
  "AdditionalKickMessage": "",
  "Kicking": {
    "CommunityBan": true,
    "TradeBan": true,
    "PrivateProfile": true,
    "ForceHoursPlayedKick": false
  },
  "LogInsteadofKick": false,
  "Thresholds": {
    "MaxVACBans": 1,
    "MaxGameBans": 1,
    "MinSteamLevel": 2,
    "MaxAccountCreationTime": -1,
    "MinGameCount": 3,
    "MinRustHoursPlayed": -1,
    "MaxRustHoursPlayed": -1,
    "MinOtherGamesPlayed": 2,
    "MinAllGamesHoursPlayed": -1
  }
}
```

Options explained:
* `ApiKey` -- The Steam Web API Key, required. Generate one here: https://steamcommunity.com/dev/apikey
* `AdditionalKickMessage` -- This will be appended to all kick-messages. E.g. you could write in a way to get whitelisted
* `CommunityBan` (`true` or `false`) -- Kick when the Player is Community-Banned
* `TradeBan` (`true` or `false`) -- Kick when the Player is Trade-Banned
* `PrivateProfile` (`true` or `false`) -- Kick when the Player has a private profile. Some checks depend on it
* `ForceHoursPlayedKick` (`true` or `false`) -- Kick the player, if gametime checks are on (e.g. MinRustHoursPlayed) - and he has his games hidden
    * A lot of users have their Steam profiles on public BUT theirs hours-played hidden (new Steam default setting?)
    * With this option on false, it will only do the hour-checks if he has the game-information on public (recommended)
* `LogInsteadofKick` (`true` or `false`) -- If true, will just log wether the player would pass the steamchecks, instead of actually kicking him on failure
* `MaxVACBans` -- If `0`, will kick if the user has any VAC Ban. If `1` will kick if the user has at least 2 VAC Bans, etc.. Use `-1` to disable check.
* `MaxGameBans` -- If `0`, will kick if the user has any Game Ban. If `1` will kick if the user has at least 2 Game Bans, etc.. Use `-1` to disable check.

Those options require a public steam profile:
* `MinSteamLevel` -- The minimum Steam level the user must have
* `MaxAccountCreationTime` In Unix time -- Accounts created after this time point will be kicked
* `MinGameCount` -- Minimum amount of games the user must have
    * If the user has hidden their hours played, it will use information from the Steam badges

Those options require, that the user has their games/gametimes visible. If `ForceHoursPlayedKick` is false, it will only kick them, if their games are visible and the constraints below are not matched.
* `MinRustHoursPlayed` in hours -- Minimum hours of Rust played 
* `MaxRustHoursPlayed` in hours -- Maximum hours of Rust played (could be useful for newbie servers)
* `MinOtherGamesPlayed` in hours -- Minimum hours of Steam games the user must have played (except rust)
    * Will only check, if the user has at least 2 steam games
* `MinAllGamesHoursPlayed` in hours -- Minimum hours of games the user must have played (including rust)

All checks do NOT include free-2-play games.

## Whitelist

Simply give your players/groups you wish to whitelist the permission `steamchecksreborn.skip`.

You can also use a group for that: `oxide.group add whitelist` and `oxide.grant group whitelist steamchecksreborn.skip` and `oxide.usergroup add <steamid64> whitelist`

## Behaviour

Players who went through the checks via joining the server (not via test commands), will be temporarly cached in a seperate white/denylist. So next time they will join, it won't do the checks again; and instead kick or let-through directly. The cache will be deleted, when the plugin reloads or the server restarts.

The plugin does the checks in this order:
1. Bans
2. Player Summaries (is profile private, account creation time)

Only when profile public:  

3. Player Level
4. Game Hours and Count
5. Game badges, to get amount of games if user has hidden Game Hours

The checks are completly asynchronous.

## Issues

If you encounter a bug, please create an GitHub issue.
Please include the output of `steamcheck <steamid64>` and `steamcheck.runtests <steamid64>`, using the steamid on which the checks don't pass correctly.

## Development

Clone the GitHub-repository. Copy the Oxide DLLs into the dep folder, and open the project with VSCode.
