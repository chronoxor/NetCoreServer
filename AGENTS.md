# AGENTS.md

This file provides guidance to AI Agents when working with code in this repository.

## What this repo is

NetCoreServer is a .NET 10 class library (`source/NetCoreServer/NetCoreServer.csproj`, package id `NetCoreServer`) that provides asynchronous socket clients and servers for TCP, UDP, SSL, Unix Domain Sockets, HTTP(S) and WebSocket(s). It also includes an optional integration with [Fast Binary Encoding](https://github.com/chronoxor/FastBinaryEncoding) via the `proto/` project. The solution `NetCoreServer.sln` aggregates the library, the `proto` project, an `examples/` project per protocol, a matching benchmark in `performance/`, and the xUnit test project in `tests/`.

## Build / test commands

`net10.0` everywhere. The repo expects the .NET 10 SDK on PATH.

```shell
# From the repo root — solution-level
dotnet restore
dotnet build --configuration Release
dotnet test NetCoreServer.sln                          # runs all xUnit tests
dotnet test --filter "FullyQualifiedName~TcpTests"     # single test class
dotnet test --filter "DisplayName~TcpServerTest"       # single test by name

# Convenience scripts
build/unix.sh           # restore + build + test (Linux / macOS)
build/vs.bat            # Windows: calls 01-build.bat / 02-tests.bat / 03-release.bat
                        # 01 uses nuget + MSBuild to /t:pack the NuGet
                        # 03 zips the library, examples and benchmarks into release/
```

The CI workflows under `.github/workflows/` only run `dotnet restore && dotnet build --configuration Release` — they do **not** run tests. Run `dotnet test` locally before pushing changes that could affect runtime behavior.

The test project copies `tools/certificates/server.pfx` and `client.pfx` to its output directory (see `tests/tests.csproj`). The SSL/HTTPS/WSS tests will fail if those files are missing or the password (`qwerty`) is changed without updating the test sources. Regenerate the full bundle with `tools/certificates/generate.sh` (or `generate.bat`) — both regenerate CA, server, client, and `dh4096.pem` in a single run.

## Architecture

All transport classes are designed as **base classes you subclass** and override `OnConnected` / `OnDisconnected` / `OnReceived` / `OnError`, etc. The examples under `examples/` are the canonical reference for how each protocol is consumed; the benchmarks under `performance/` mirror them but tuned for throughput.

The class hierarchy in `source/NetCoreServer/` is layered — higher protocols extend lower ones rather than wrap them:

- **TCP / UDP / UDS** — `TcpServer` + `TcpSession` + `TcpClient`, plus UDP and Unix-Domain-Socket equivalents. These are the leaf transports; everything else builds on TCP.
- **SSL** — `SslServer : TcpServer`, `SslSession : (its own type, not TcpSession)`, `SslClient`. Note `SslSession` is **not** derived from `TcpSession`; the SSL stack reimplements the session loop on top of `SslStream`. Be careful when porting changes between `TcpSession.cs` and `SslSession.cs` — they intentionally duplicate logic.
- **HTTP** — `HttpServer : TcpServer`, `HttpSession : TcpSession`, `HttpClient : TcpClient`. `HttpRequest` / `HttpResponse` are the protocol DTOs; `FileCache` is the in-memory static-content cache attached to `HttpServer.Cache`.
- **HTTPS** — `HttpsServer : SslServer`, `HttpsSession : SslSession`, `HttpsClient : SslClient`. Mirrors HTTP but over SSL.
- **WebSocket** — `WsServer : HttpServer`, `WsSession : HttpSession`, `WsClient : HttpClient`. The HTTP upgrade handshake is intercepted on top of the HTTP layer, then framing is delegated to the shared `WebSocket` helper class (which implements `IWebSocket`). `WssServer` / `WssSession` / `WssClient` do the same on top of HTTPS.

Cross-cutting helpers:

- `Buffer.cs` — the custom growable byte buffer used by every session for receive/send pipelines (two send buffers: "main" + "flush", swapped under lock).
- `WebSocket.cs` / `IWebSocket.cs` — WS framing, masking, ping/pong, and `OnWsConnecting()` hook (used to customize the UPGRADE response — see the recent commit).
- `SslContext.cs` — wraps `X509Certificate2` plus `SslProtocols` selection for the SSL/HTTPS/WSS family.
- `FileCache.cs`, `StringExtensions.cs`, `Utilities.cs` — supporting utilities.

All session and server classes document themselves as `<remarks>Thread-safe</remarks>` — the receive/send paths use interlocked counters plus `lock` on internal buffers. When adding new state, follow the existing pattern rather than introducing a new sync primitive.

## Proto / FBE integration

`proto/com.chronoxor.fbe.cs` and `proto/com.chronoxor.simple.cs` are **generated** from `proto/simple.fbe` by the external `fbec` tool (command in the file header: `fbec --csharp --proto --input=simple.fbe --output=.`). Do not hand-edit the generated `.cs` files — change the `.fbe` schema and regenerate. The `ProtoServer` / `ProtoClient` examples and benchmarks consume this generated code.

## Conventions worth knowing

- The library targets `net10.0` only. Do not add multi-targeting or polyfills without coordinating — the README still lists .NET 6 as a requirement but the actual TFM has moved on.
- XML doc comments are generated into the NuGet (`GenerateDocumentationFile=true`, `NoWarn=1591`). Public members get `<summary>` blocks; matching the existing style keeps the package documentation consistent.
- The repo has no linter / formatter config — match surrounding style (Allman braces, 4-space indent, `_camelCase` private fields).
- Example and performance projects are intentionally minimal (one `Program.cs` each). New protocols should follow the same shape: subclass the base, expose a tiny CLI in `Program.cs`.
