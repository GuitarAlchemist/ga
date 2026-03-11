---
status: pending
priority: p2
issue_id: "046"
tags: [code-review, security, ssrf, fsharp, dsl]
---

# IO Closures: SSRF via Unrestricted httpGet / httpPost

## Problem Statement
`io.httpGet` and `io.httpPost` in `IoClosures.fs` use `new HttpClient()` with no URL allowlist. This allows Server-Side Request Forgery (SSRF) to cloud metadata endpoints such as `169.254.169.254` (AWS/GCP/Azure instance metadata), internal services, and loopback addresses.

## Proposed Solution
- Replace `new HttpClient()` with `IHttpClientFactory` (injected, named client)
- Add a URL validation step before issuing any request:
  - Block link-local ranges (`169.254.0.0/16`)
  - Block loopback (`127.0.0.0/8`, `::1`)
  - Block private ranges (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`) unless explicitly permitted
- Add an `AllowedDomains` configuration list; reject requests to unlisted hosts
- Return `Result<string, HttpError>` rather than throwing on network failure

**File:** `Common/GA.Business.DSL/Closures/BuiltinClosures/IoClosures.fs`

## Acceptance Criteria
- [ ] `io.httpGet` and `io.httpPost` use `IHttpClientFactory`, not `new HttpClient()`
- [ ] Requests to `169.254.x.x` are blocked before a socket is opened
- [ ] Requests to loopback addresses are blocked
- [ ] Only domains in the configured allowlist are reachable
- [ ] Blocked requests return a structured error, not a thrown exception
- [ ] Unit tests cover: allowed domain, metadata endpoint, loopback, private IP
