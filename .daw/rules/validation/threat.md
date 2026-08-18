---
applyTo: '**'
version: 1.0.1
---

# 3. Validación del threat model (`daw-threat-modeling`)

> Parte del catálogo de validación de DAW. Las reglas transversales (FAIL vs WARNING,
> modificador QUICK-FIX, formato del informe) están en `.daw/rules/validation/common.md`:
> cárgalo junto con este archivo.

**Applies in:** the PLAN phase.
**Basis:** STRIDE (Microsoft), OWASP Threat Modeling, OWASP ASVS, ISO 27001 (security risk
management).

### FAIL rules

| ID | Check | Precise description | Basis |
|---|---|---|---|
| F-TM-01 | Component with no STRIDE analysis | Every component or service the spec introduces or modifies must be evaluated against the 6 STRIDE categories (Spoofing, Tampering, Repudiation, Information Disclosure, DoS, Elevation of Privilege). If a component has no analysis → FAIL. | STRIDE is the de facto standard for threat modeling. An unanalyzed component is an invisible attack surface. Microsoft SDL requires it for every new feature. |
| F-TM-02 | Unidentified trust boundary | Every interface between components with different trust levels must be identified as a trust boundary (e.g. client→server, server→DB, API→external service). If there are interfaces between trust zones with none declared → FAIL. | Vulnerabilities happen at trust boundary crossings. If you do not identify them, you do not protect them. OWASP Threat Modeling Cheat Sheet. |
| F-TM-03 | Threat with no mitigation | Every identified threat must have a documented mitigation OR an accepted risk with formal approval. If a threat is listed with neither mitigation nor acceptance → FAIL. | An identified but untreated threat is worse than an unidentified one: it proves the risk was known and ignoring it was a choice. ISO 27001 §6.1.2: risk treatment. |
| F-TM-04 | Accepted risk with no approval | If a threat is marked "accepted risk", it must include: (1) who accepted it, (2) the justification, (3) the conditions under which it will be reviewed. If any of the three is missing → FAIL. | ISO 27001 §6.1.3: accepting risk requires approval from the person with authority. A developer cannot self-accept a security risk without the user/owner validating it. |
| F-TM-05 | Sensitive data with no classification | If the spec handles user data, credentials, tokens, or financial information, the threat model must classify it (PII, credentials, financial, public). If there is unclassified sensitive data → FAIL. | You cannot protect what you have not classified. GDPR Art. 32, OWASP ASVS V8: Data Protection. The classification determines the required controls (encryption, access, retention). |
| F-TM-06 | Generic threat model | The threat model must reference the spec's real architecture (components, endpoints, specific data flows). If the threat model is a generic template unrelated to the concrete design → FAIL. | A threat model that does not reflect the real system is security theater. It protects nothing. |
| F-TM-07 | Sensitive data with no encryption | If the threat model identifies data classified as PII or credentials, it must specify encryption at rest and in transit. If it does not → FAIL. | OWASP ASVS V6: Cryptography. GDPR Art. 32: appropriate technical measures. Storing PII unencrypted is a regulatory violation in most jurisdictions. |

### WARNING rules

| ID | Check | Precise description | Basis |
|---|---|---|---|
| W-TM-01 | No dependency analysis | The threat model does not analyze supply chain risks (third-party dependencies). | Relevant but not always applicable if no new dependencies are added. OWASP A06: Vulnerable Components. |
| W-TM-02 | No availability analysis | The threat model does not analyze DoS vectors for services that are non-critical or internal-only. | DoS is part of STRIDE, but for low-traffic internal services it may be a minor risk. |

