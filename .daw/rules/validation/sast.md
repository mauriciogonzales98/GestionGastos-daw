---
applyTo: '**'
version: 1.0.1
---

# 4. Validación SAST (`daw-security-sast`)

> Parte del catálogo de validación de DAW. Las reglas transversales (FAIL vs WARNING,
> modificador QUICK-FIX, formato del informe) están en `.daw/rules/validation/common.md`:
> cárgalo junto con este archivo.

**Applies in:** the CODE phase (blocking gate).
**Basis:** OWASP Top 10 (2021), CWE Top 25, OWASP ASVS v4, NIST SP 800-53.

### 4.1 Finding Severity Classification

SAST findings are classified by severity. The severity determines the disposition:

| Severity | Disposition | Can be suppressed | Condition for suppression |
|---|---|---|---|
| **Critical** | **FAIL — always blocks** | No | None. It must be fixed. |
| **High** | **FAIL — always blocks** | No | None. It must be fixed. |
| **Medium** | **FAIL — blocks by default** | Yes, with documentation | A false positive documented with justification + reviewer, OR an accepted risk with the user's approval. |
| **Low** | **WARNING** | N/A (does not block) | Reported and documented. Does not block. |
| **Informational** | **WARNING** | N/A (does not block) | Reported. Does not block. |

### 4.2 Categories that are ALWAYS FAIL

Regardless of tool or context, these findings are ALWAYS FAIL (Critical/High):

| ID | Category | CWE | Severity | Basis |
|---|---|---|---|---|
| F-SAST-01 | Hardcoded secrets | CWE-798 | Critical | Credentials in source code persist in git history forever. OWASP A07. |
| F-SAST-02 | SQL injection | CWE-89 | Critical | Direct database compromise. OWASP A03. |
| F-SAST-03 | OS command injection | CWE-78 | Critical | Remote code execution on the server. OWASP A03. |
| F-SAST-04 | Insecure deserialization | CWE-502 | Critical | Remote code execution vector. OWASP A08. |
| F-SAST-05 | Path traversal | CWE-22 | High | Access to system files outside the allowed directory. OWASP A01. |
| F-SAST-06 | XSS (Reflected or Stored) | CWE-79 | High | Code execution in other users' browsers. OWASP A03. |
| F-SAST-07 | SSRF | CWE-918 | High | Access to the internal network and cloud metadata. OWASP A10. |
| F-SAST-08 | Broken cryptography | CWE-327 | High | MD5, SHA1 for passwords, DES, ECB mode. Passwords compromisable in a breach. OWASP A02. |
| F-SAST-09 | Debug mode in production | CWE-489 | High | Exposes stack traces, internal configuration, and possibly code execution. OWASP A05. |
| F-SAST-10 | Logging sensitive data | CWE-532 | High | Passwords, tokens, PII in logs = a data breach via logs. OWASP A09. |
| F-SAST-11 | Unrestricted upload | CWE-434 | High | Uploading executables, webshells. OWASP A04. |
| F-SAST-12 | Missing CSRF protection | CWE-352 | High | State-changing operations without CSRF protection in web apps. OWASP A01. |
| F-SAST-13 | Critical/High CVE in a dependency | — | Critical/High | A public exploit is available. OWASP A06. |

### 4.3 Medium categories (FAIL, suppressible with documentation)

| ID | Category | Example | Basis |
|---|---|---|---|
| F-SAST-14 | Incomplete input validation | Input accepted without validating type/length/format. | CWE-20. Depends on context: internal vs external input. |
| F-SAST-15 | Insecure error handling | A generic catch that exposes internal details. | CWE-209. The impact depends on what information is exposed. |
| F-SAST-16 | Medium CVE in a dependency | Known vulnerability, non-trivial exploit. | OWASP A06. Assess whether the vulnerable function is used. |
| F-SAST-17 | Unsafe function | Use of `eval()`, `exec()`, deserialization functions without context. | CWE-95. Depends on whether the input is controlled. |

### 4.4 Finding Suppression Protocol

**Every finding, including false positives, must be documented.** An undocumented finding is an
unreviewed finding.

To suppress a Medium finding as a false positive or an accepted risk:

```markdown
### Suppression: [finding ID]

| Field | Value |
|---|---|
| File | [path:line] |
| Category | [finding category] |
| Disposition | FALSE_POSITIVE / ACCEPTED_RISK |
| Reviewer | [name of the user who reviewed it] |
| Date | [review date] |
| Justification | [1–3 sentences explaining why it is not exploitable, or why it is accepted] |
| Compensating control | [ACCEPTED_RISK only: which other control mitigates the risk] |
| Review by | [latest date to re-evaluate, at most 6 months out] |
```

**Suppression rules:**

| ID | Check | Severity |
|---|---|---|
| F-SAST-18 | Every suppression must have all 7 fields filled in. If any is missing → FAIL. | FAIL |
| F-SAST-19 | Suppressions must be reviewed when SAST is re-run. If a suppression is more than 6 months old → FAIL (it must be re-evaluated). | FAIL |
| W-SAST-01 | Low or Informational finding left undocumented. | WARNING |

