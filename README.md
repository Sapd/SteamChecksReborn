Checks connecting users for configurable criteria of the Steam WebAPI and kicks if not fulfilled.  

If you upgrade from the previous versions (< 5), please back-up your old config and delete it; like-wise the language file in oxide/lang/en etc.

## Permissions

* `steamchecks.use`  -- Allows to issue test commands
* `steamchecks.skip` -- Users with this permission, won't be checked on connect

## Console Commands

* `steamcheck <steamid64>` -- Checks the given steamid64 (does not matter if connected), for all configured criteria and returns wether he would have been kicked
* `steamcheck.runtests <steamid64>` -- Calls all WebAPI functions with the given steamid64 and returns detailed output. Use this output when creating an issue

## Configuration

```json
{
  "AdditionalKickMessage": "",
  "ApiKey": "",
  "CacheDeniedPlayers": false,
  "CachePassedPlayers": true,
  "Kicking": {
    "CommunityBan": true,
    "TradeBan": true,
    "PrivateProfile": true,
    "LimitedAccount": true,
    "NoProfile": true,
    "FamilyShare": false,
    "ForceHoursPlayedKick": false
  },
  "LogInsteadofKick": false,
  "Thresholds": {
    "MaxVACBans": 1,
    "MinDaysSinceLastBan": -1,
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

##### Options explained

* `ApiKey` -- The Steam Web API Key, required. Generate one here: https://steamcommunity.com/dev/apikey
* `AdditionalKickMessage` -- This will be appended to all kick-messages. E.g. you could write in a way to get whitelisted
* `CachePassedPlayers` -- Don't check players again, which passed the checks
    * Cache resets on plugin-reload / server restart
* `CacheDeniedPlayers` -- Don't check players again, which failed checks and kick directly
    * Cache resets on plugin-reload / server restart
    * Default is false, as players can't join if their have their profile on private - and then try to re-join with the same profile set to public
* `CommunityBan` (`true` or `false`) -- Kick when the player is community-banned
* `TradeBan` (`true` or `false`) -- Kick when the player is trade-banned
* `PrivateProfile` (`true` or `false`) -- Kick when the player has a private profile. Most checks depend on it
* `LimitedAccount` (`true` or `false`) -- Kick when the player has a limited account
* `NoProfile` (`true` or `false`) -- Kick when the player has his steam community profile not yet set up
* `ForceHoursPlayedKick` (`true` or `false`) -- Kick the player, if hours played checks are on (e.g. MinRustHoursPlayed) - and he has his games hidden
    * Note: A lot of users have their Steam profiles on public BUT theirs hours-played hidden (new Steam default setting)
    * With this option being false, it will only do the hour-checks if he has the hours-played-information on public (recommended)
* `LogInsteadofKick` (`true` or `false`) -- If true, will just log whether the player would pass the steamchecks, instead of actually kicking him on failure
* `MaxVACBans` -- If `0`, will kick if the user has any VAC Ban. If `1`, will kick if the user has at least 2 VAC Bans, etc.. Use `-1` to disable check.
* `MinDaysSinceLastBan` -- Minimum Number of days since the last ban. Use `-1` to disable check.
    * This option only makes sense, when `MaxVACBans` is greater than 0 or disabled with -1.
* `MaxGameBans` -- If `0`, will kick if the user has any Game Ban. If `1`, will kick if the user has at least 2 Game Bans, etc.. Use `-1` to disable check.

###### Those options require a **public** steam profile:
* `MinSteamLevel` -- The minimum Steam level the user must have
    * A steam level of 1 or higher also excludes all limited accounts
* `MaxAccountCreationTime` In Unix time -- Accounts created after this time point will be kicked
* `MinGameCount` -- Minimum amount of games the user must have
    * If the user has hidden their hours played, it will use information from the Steam badges

###### Those options require, that the user has their games/gametimes visible. If `ForceHoursPlayedKick` is false, it will only kick them, if their games are visible and the constraints below are not matched.
* `MinRustHoursPlayed` in hours -- Minimum hours of Rust played 
* `MaxRustHoursPlayed` in hours -- Maximum hours of Rust played (could be useful for newbie servers)
* `MinOtherGamesPlayed` in hours -- Minimum hours of Steam games the user must have played (except rust)
    * Will only check, if the user has at least 2 steam games
* `MinAllGamesHoursPlayed` in hours -- Minimum hours of games the user must have played (including rust)

All checks do NOT include free-2-play games. You can disable checks with `-1`.

## Whitelist

Simply give your players/groups you want to whitelist the permission `steamchecks.skip`

You can also use a group for that:  
`oxide.group add whitelist` -- Add whitelist group  
`oxide.grant group whitelist steamchecks.skip` -- Add permission to whitelist group  
`oxide.usergroup add <steamid64> whitelist` -- Add player to whitelist group  

## Behaviour

The plugin does the checks in this order:
1. Bans
2. Is game lended (family share)
3. Player Summaries (is profile private, account creation time)
    * Limited Profile and Steam-Commmunty profile

Only when profile public:  

4. Player Level
5. Game Hours and Count
6. Game badges, to get amount of games
    - Only done if the user has his game hours hidden

The checks are completly asynchronous.

## Issues

If you encounter a bug, please create an GitHub issue.  
Please include the output of `steamcheck <steamid64>` and `steamcheck.runtests <steamid64>`, using the steamid on which the checks don't pass correctly. If you have an error, also include the offending steamids. Also your configuration file would be helpful.

## Development

Clone the GitHub-repository. Copy the Oxide DLLs into the dep folder, and open the project with VSCode.