# Moffstation Discord Linking

This website is used to link Discord accounts to users' information on the Moffstation server. It does this by having
users log in with their SS14 account via OIDC and then logging in with their Discord account via the Discord API, and
then prompts the user to link their accounts by pressing a button which just inserts a value into the Moffstation Player
DB extension row that is their Discord ID. This simply allows us to conflate a Discord identity with a Moffstation
Player (and an SS14 account).

## Releases

Releases are created automatically when a version tag is pushed to the repository. The release workflow:

1. Builds and pushes a Docker image to the
   [GitHub Container Registry](https://github.com/Centronias/ss14-recover/pkgs/container/ss14-recover)
2. Creates a GitHub Release (only if the Docker publish succeeds) with auto-generated release notes
   and a link to the Docker image

To cut a release:

```sh
git tag v1.2.3
git push origin v1.2.3
```

The Docker image will be tagged with the full version (`1.2.3`), minor version (`1.2`), and major version (`1`).
Pushes to `master` do **not** trigger any publishing — only `v*` tags do.

## Configuration

### SS14 (OpenID Connect)

Register an OIDC client at `https://account.spacestation14.com/Identity/Account/Manage/Developer`
using `https://<your-domain>/signin-oidc` as the redirect URI. The resulting `ClientId` and `ClientSecret`
go into the `Auth:` block below.

### Discord

1. In the [Discord Developer Portal](https://discord.com/developers/applications), create an application.
2. Under **OAuth2 -> Redirects**, add `https://<your-domain>/discord-callback`.
3. The **Client ID** and **Client Secret** go into the `Discord:` block below.

You also need a bot for user lookups: under **Bot**, create a bot and copy the token into `Discord:BotToken`.
The bot does not need to join any server, it just exists to be an API token :)

### Example config

```yml
Serilog:
  Using: [ "Serilog.Sinks.Console" ]
  MinimumLevel:
    Default: Information
    Override:
      SS14: Debug
      Microsoft: "Warning"
      Microsoft.Hosting.Lifetime: "Information"
      Microsoft.AspNetCore: Warning
      IdentityServer4: Warning
  WriteTo:
  - Name: Console
    Args:
    OutputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}"

  Enrich: [ "FromLogContext" ]

  #Loki:
  #  Address: "http://localhost:3102"
  #  Name: "centcomm"

ConnectionStrings:
  # Connects to the same postgres database as the game server
  DefaultConnection: "Server=127.0.0.1;Port=5432;Database=ss14;User Id=ss14-admin;Password=foobar"

AllowedHosts: "central.spacestation14.io"

urls: "http://localhost:27689/"

PathBase: "/admin"

WebRootPath: "/opt/ss14_admin/bin/wwwroot"

ForwardProxies:
- 127.0.0.1
- 172.16.0.0/12  # Supports CIDR notation for subnets  (Docker)

Auth:
  Authority: "https://central.spacestation14.io/web/"
  ClientId: "9e2ce26f-28ba-4232-b4d9-8cc08993b33e"
  ClientSecret: "foobar"

authServer: "https://central.spacestation14.io/auth"

Discord:
  ClientId: "123456789012345678"
  ClientSecret: "foobar"
  BotToken: "Bot foobar"
```

## Credits

This website is MASSIVELY based on the [SS14.Admin](https://github.com/space-wizards/SS14.Admin) website -- so much so
that its code was directly forked from that project at commit b5ab86060314f70c4bb9a174f9b47653dc8b6503. To that end, the
commits in this repository at that commit and earlier than it (ie. 2026-01-23) are licensed by the Space Station 14
Contributors rather than the Moffstation Contributors. All licensing is under the MIT license.
See more in [the license](./LICENSE.txt).
