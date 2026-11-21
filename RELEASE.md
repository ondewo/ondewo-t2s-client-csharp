# Release History

*****************

## Release ONDEWO T2S Csharp Client 6.7.1

### New Features

* [[OND236-120]](https://ondewo.atlassian.net/browse/OND236-120) Offline-token start-up, as the python SDK has it: a handed-off Keycloak OFFLINE refresh token (`refreshToken`) replaces the password. The provider then starts with ONE `grant_type=refresh_token` exchange and never sends a password grant, not even when the token is refused, because Keycloak's brute-force detection counts failed password logins and N processes sharing one technical user would lock it. A Keycloak config needs `password` or `refreshToken`; with both, the refresh token wins.
* [[OND236-120]](https://ondewo.atlassian.net/browse/OND236-120) `OndewoClientConfig` takes `keycloakUrl`, `realm`, `clientId`, `username`, `password`, `refreshToken` and `tokenExpirationInS`, and `OndewoChannelFactory.Create` attaches an auto-refreshed bearer token to every call on the channel (namespace `Ondewo.T2S.Client.Auth`: `KeycloakTokenProvider`, `KeycloakTokenProviderRegistry`).

### Bug Fixes

* [[OND236-120]](https://ondewo.atlassian.net/browse/OND236-120) A NaN backoff jitter counts as 0 (a 5 s delay) instead of producing a NaN delay; an infinite jitter is clamped to the backoff bounds.

### Improvements

* [[OND236-120]](https://ondewo.atlassian.net/browse/OND236-120) A failed token refresh never ends the refreshing: every failure (an unreachable or timed-out token endpoint, a non-2xx answer, a body that is not a JSON object, a missing `access_token`, a non-numeric `expires_in`) is retried with a full-jitter backoff from 5 s doubling up to 300 s, which the background refresh and the per-call refresh share, so a Keycloak outage costs one token request per backoff step instead of one per call. A success resets the backoff.
* [[OND236-120]](https://ondewo.atlassian.net/browse/OND236-120) A token that is still valid keeps being served while its refresh fails; only an EXPIRED token that cannot be refreshed fails, with a Keycloak authentication error that chains the underlying cause. A malformed token response changes no provider state, and an error about a response without an `access_token` names its field names only, never their values.
* [[OND236-120]](https://ondewo.atlassian.net/browse/OND236-120) One provider is shared per credential set: the registry key is a SHA-256 over the Keycloak connection and credential fields (the password and the refresh token enter it only as hashes), so two configs of different tenants never share a provider and all clients of one identity log in once.
* [[OND236-120]](https://ondewo.atlassian.net/browse/OND236-120) The token request times out after 10 s, quoted Keycloak error bodies are cut at 500 characters, each failed attempt logs one WARNING (attempt number, retry delay, remaining token validity) and the first success afterwards one INFO; no error message and no log line carries a password or a token.
* [[OND236-120]](https://ondewo.atlassian.net/browse/OND236-120) The behaviour is pinned by the same test catalogue as the python SDK (`test_keycloak_refresh_resilience.py`, `test_client_config_refresh_token.py`), plus edge cases: every failure kind, the backoff ladder and its bounds, concurrent callers during an outage (one token request), malformed responses, credential-free logs and the credential-keyed registry.

*****************

## Release ONDEWO T2S Csharp Client 6.7.0

### Improvements

* Tracking API Version [6.7.0](https://github.com/ondewo/ondewo-t2s-api/releases/tag/6.7.0) ( [Documentation](https://ondewo.github.io/ondewo-t2s-api/) )
* [[OND236-120]](https://ondewo.atlassian.net/browse/OND236-120) Generated with ondewo-proto-compiler 5.17.0 (protoc 35.1). New runtime floors: Google.Protobuf 3.35.1, Grpc.Core.Api / Grpc.Net.Client 2.85.0, Google.Api.CommonProtos 2.17.0.

*****************

## Release ONDEWO T2S Csharp Client 6.6.1

### New Features

* TLS and mutual TLS: `OndewoClientConfig` and `OndewoChannelFactory.Create` (namespace
  `Ondewo.T2S.Client.Connection`) build the `GrpcChannel` the generated service clients take, with the rules of the
  ONDEWO Python client. **They exist only in the new `net10.0` target of the package**; the `netstandard2.0`
  target (.NET Framework, older .NET) does not contain them, because `SocketsHttpHandler`,
  `SslClientAuthenticationOptions` and `X509Certificate2.CreateFromPem` do not exist there.
  * `grpcCert`, `grpcClientCert` and `grpcClientKey` hold PEM content, not file paths; a certificate field
    without a PEM certificate in it is refused with `ArgumentException`.
  * With `grpcCert` set only those CAs are trusted, otherwise the platform trust store; the host is always
    checked against the certificate's SAN.
  * `grpcClientCert` / `grpcClientKey` are both-or-neither (refused in the constructor); empty strings on both
    mean plain TLS. The key must be an unencrypted PEM key (PKCS#8, PKCS#1 or SEC1).
  * `useSecureChannel: false` with a client identity is refused; a plaintext channel logs a warning naming
    `host:port` through `GrpcChannelOptions.LoggerFactory`.
  * No exception message renders a PEM or the key; `OndewoClientConfig.ToString()` redacts the key.
  * Bare IPv6 literal hosts are bracketed; CRLF PEMs work.
  * Channel defaults: keepalive ping every 30 s with a 20 s ping timeout, only while calls are active; 5 s maximum
    reconnect backoff; 2^31-1 maximum message size. Not configured (documented in the README): a per-method retry
    policy, `max_pings_without_data` and `TCP_USER_TIMEOUT`, which `Grpc.Net.Client` does not expose.

### Improvements

* The package now targets `netstandard2.0` and `net10.0`. The `netstandard2.0` target is unchanged: the
  generated stubs and `OndewoAuth`.
* README: new section "TLS, mutual TLS and certificates" (modes, rules, example, channel defaults, a test PKI
  with openssl, security notes, troubleshooting).
* Tests: real TLS and mutual TLS handshakes against an in-process Kestrel server with a PKI built at test time
  (custom CA, client chain, CRLF, IPv6, missing / unrelated client identity, wrong CA, wrong host name, system
  trust store, plaintext), and a test that pins the release-notes slice of `RELEASE.md` the GitHub release is
  cut from.
* The stubs are generated by [ondewo-proto-compiler 5.15.2](https://github.com/ondewo/ondewo-proto-compiler/releases/tag/5.15.2)
  (previously 5.15.1).
* Tracking API Version [6.6.0](https://github.com/ondewo/ondewo-t2s-api/releases/tag/6.6.0) ( [Documentation](https://ondewo.github.io/ondewo-t2s-api/) )

*****************

## Release ONDEWO T2S Csharp Client 6.6.0

### New Features

* Initial release of the ONDEWO T2S C# client. The package `Ondewo.T2S.Client` ships the
  gRPC message and client stubs generated from the [ONDEWO T2S API](https://github.com/ondewo/ondewo-t2s-api)
  by the `ondewo-csharp-proto-compiler` image of
  [ondewo-proto-compiler 5.15.1](https://github.com/ondewo/ondewo-proto-compiler): one
  `<Message>.cs` per proto plus one `<Service>Grpc.cs` per proto that declares a service, nested by C# namespace
  under `api/`.
* The package targets `netstandard2.0`, so it is consumable from .NET Framework 4.6.1+, .NET Core 2.0+ and every
  modern .NET, and carries `Google.Protobuf`, `Grpc.Net.Client`, `Grpc.Core.Api` and `Google.Api.CommonProtos` as
  its only dependencies. The `google/api`, `google/rpc` and `google/type` descriptors the API imports come from
  `Google.Api.CommonProtos` rather than being generated a second time into this assembly.
* `make build` is the whole pipeline — submodule checkout at the pinned versions, compiler image build, stub
  generation and `dotnet pack` — and `make ondewo_release` cuts the GitHub and NuGet release from the same
  version number.

*****************
