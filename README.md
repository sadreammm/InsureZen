# InsureZen API

A .NET REST API for digitising and streamlining medical insurance claim processing. The system implements a two-stage human review workflow (Maker → Checker) on behalf of multiple insurance companies.

---

## Table of Contents

- [Prerequisites](#prerequisites)
- [Running the Application](#running-the-application)
- [Running Tests](#running-tests)
- [Task 1 – Requirements Analysis](#task-1---requirements-analysis)
- [Task 2 – API Design](#task-2---api-design)
- [Assumptions](#assumptions)

---

## Prerequisites

- [Docker](https://www.docker.com/) and Docker Compose
- [.NET](https://dotnet.microsoft.com/en-us/download) 

---

## Running the Application

```bash
git clone https://github.com/sadreammm/InsureZen.git
cd InsureZen/InsureZenAPI
```

Create a `.env` file in `InsureZenAPI/` folder with the same contents as `InsureZenAPI/.env.example`. Then

```bash
docker-compose up --build
```

The API will be available at `http://localhost:8080`.  
The Scalar API explorer is available at `http://localhost:8080/scalar`.

To stop the application:

```bash
docker-compose down
```

---

## Running Tests

```bash
cd ../InsureZenAPI.Tests
dotnet test
```


The test suite covers claim state transition logic.

---

# Task 1 - Requirements Analysis

## Entities

**User Entity**

| **Data Point** | **Types**                       |
| -------------- | ------------------------------- |
| UserId         | UUID (PK)                       |
| Name           | String                          |
| Role           | Enum (Employee, Maker, Checker) |

`Role` field has been used to clearly distinguish between a Maker and a Checker, and an Employee role is a general role.

**InsuranceCompany Entity**

| **Data Point** | **Types** |
| -------------- | --------- |
| CompanyId      | UUID (PK) |
| CompanyName    | String    |

**Claim Entity**

| **Data Point**      | **Types**                                                                          |
| ------------------- | ---------------------------------------------------------------------------------- |
| ClaimId             | UUID (PK)                                                                          |
| CompanyId           | UUID (FK)                                                                          |
| Status              | Enum (Pending, Maker_In_Progress, Checker_Pending, Checker_In_Progress, Completed) |
| RawClaimData        | String                                                                             |
| NormalizedClaimData | String                                                                             |
| ReviewerId          | UUID (FK)                                                                          |
| SubmittedAt         | Datetime                                                                           |
| CompletedAt         | Datetime                                                                           |

`RawClaimData` is stored as string as the claim data could be in any format depending on the insurance companies.

`NormalizedClaimData` is also stored as string for the same reason. The normalization logic (not implemented in this case) will convert the raw claim into JSONB using the company specific. 

`ReviewerId` holds the current maker or checker’s Id to ensure no other concurrent maker or checker can review a claim.

**Review Entity**

| **Data Point**      | **Types**                 |
| ------------------- | ------------------------- |
| ReviewId            | UUID (PK)                 |
| ClaimId             | UUID (FK)                 |
| MakerId             | UUID (FK)                 |
| MakerFeedback       | String                    |
| MakerRecommendation | Enum (Approve, Reject)    |
| MakerSubmittedAt    | Datetime                  |
| CheckerId           | UUID (FK)                 |
| FinalDecision       | Enum (Approved, Rejected) |
| CheckerSubmittedAt  | Datetime                  |

## Actors & Roles

- **Insurance Company** - Submits insurance claims to InsureZen and receives the final processed claim with decision.
- **Processing Service** - Receives the raw claim, extracts and normalized the raw claim into a structured unified format.
- **Maker** - Performs the initial human review of a claim. Annotates with feedback, sets a recommendation and submits it for Checker review.
- **Checker** - Performs the final review based on the claim data and Maker's recommendation, then issues the final decision which is forwarded to the insurance company.

## Functional Requirements

**Claim Ingestion**

| ID        | Requirement                                                                                                   |
|-----------|---------------------------------------------------------------------------------------------------------------|
| **FR-CI-01** | The external company must be able to submit a claim via the API, providing the Company Id and raw claim data.        |
| **FR-CI-02** | The system must store both the raw claim data and normalized data derived from it.                                   |
| **FR-CI-03** | On submission, the system must return a unique claim Id and set the claim status to pending for Makers to access it. |

**Maker Flow**

| ID        | Requirement                                                                                                   |
|-----------|---------------------------------------------------------------------------------------------------------------|
| **FR-MF-01** | A Maker must be able to retrieve all claims currently in `Pending` status.                                                                                 
| **FR-MF-02** | A Maker must be able to accept a claim, thereby changing the claim status to `Maker_In_Progress`, hence preventing other Makers from accepting it.                                                                                  |
| **FR-MF-03** | A Maker must be able to view full claim details for a claim they have accepted.                                                                                                                                                     |
| **FR-MF-04** | A Maker must be able to submit a review, providing feedback and a recommendation for assisting the Checker. On submission, a new Review record is created, and claim status is changed to `Checker_Pending` for Checkers to accept. |

**Checker Flow**

| ID        | Requirement                                                                                                   |
|-----------|---------------------------------------------------------------------------------------------------------------|
| **FR-CF-01** | A Checker must be able to retrieve all claims currently in `Checker_Pending` status and the associated Maker recommendation.                             |
| **FR-CF-02** | A Checker must be able to accept a claim, thereby changing the claim status to `Checker_In_Progress`, hence preventing other Checkers from accepting it. |
| **FR-CF-03** | A Checker must be able to view full claim details and Maker's recommendation for a claim they have accepted.                                             |
| **FR-CF-04** | A Checker must be able to submit a final decision, thereby completing the claim and forward this to the insurance company.                               |

**Claims History**

| ID        | Requirement                                                                                                   |
|-----------|---------------------------------------------------------------------------------------------------------------|
| **FR-CH-01** | The system must provide a paginated claim history.                              |
| **FR-CH-02** | The system must support filtering by claim status, company name and date range. |

## Non-Functional Requirements

#### Usability
| ID     | Requirement                                                                              |
|--------|------------------------------------------------------------------------------------------|
| NFR-01 | All API responses must use consistent response structures and meaningful HTTP status codes.  |
| NFR-02 | The system must provide appropriate and descriptive error messages.                |

#### Reliability
| ID     | Requirement                                                                                                        |
|--------|--------------------------------------------------------------------------------------------------------------------|
| NFR-03 | Data Integrity - The system must use database transactions to ensure that a claim is never left in partial state.     |
| NFR-04 | Availability - The system must be highly available to handle hundreds to thousands of claims per day.    |
| NFR-05 | Auditability - The claim state transitions and claim reviews must be logged with timestamps and user ids.   |

#### Performance
| ID     | Requirement                                                                                                             |
|--------|-------------------------------------------------------------------------------------------------------------------------|
| NFR-06 | Concurrency - The system must support multiple concurrent Makers and Checkers, ensuring no two Makers or Checkers can review the same claim.               |
| NFR-07 | Response Times - Paginated history queries must be efficient and filter fields in the database must be indexed.           |

#### Supportability
| ID     | Requirement                                                                                     |
|--------|-------------------------------------------------------------------------------------------------|
| NFR-08 | The codebase must be modular and scalable. It should use Docker and Docker-Compose to support reproducibility.                        |


## Edge Cases

| ID       | Requirement                                                                                    |
|----------|------------------------------------------------------------------------------------------------|
| **EC-01** | A Maker tries to accept a claim that is not in `Pending` state, the API returns a 400 Bad Request thereby preventing concurrent makers from accepting the same claim. |
| **EC-02** | A Checker tries to view or submit a claim they did not accept, the API returns a 403 Forbidden                                                                        |
| **EC-03** | Two Makers concurrently attempt to accept the same claim, only the first write succeeds (atomicity).                                                                  |

# Task 2 - API Design

## Claims Endpoint

#### POST /api/claims — Submit a Claim

| Field         | Value                                                                                     |
|---------------|-------------------------------------------------------------------------------------------|
| **Method**        | POST                                                                                      |
| **Path**          | /api/claims                                                                               |
| **Request Body**  | `{ "companyId": UUID, "rawClaimData": String, "normalizedClaimData": String? }`          |
| **Response Shape** | `{"Message": String, "ClaimId": UUID, "Status": String, "SubmittedAt": Datetime}` |
| **Status Codes** | `201 Created` - Claim Successfully Submitted<br>`400 Bad Request` - Invalid company Id or missing required fields. |

#### GET /api/claims/{id} — Get Claim By Id

| Field        | Value                        |
|--------------|------------------------------|
| **Method**       | GET                      |
| **Path**         | /api/claim/{id}              |
| **Request Body** | None                     |
| **Response Shape** | Full claim object |
| **Status Codes** | `200 OK` - Claim Successfully Retrieved<br>`404 Not Found` - Claim Not Found |

#### GET /api/claims — Get All Claims

| Field        | Value                    |
|--------------|--------------------------|
| **Method** | GET |
| **Path** | /api/claims |
| **Request Body** | None |
| **Response Shape** | Array of all claims |
| **Status Codes** | `200 OK` - Claims Successfully Retrieved |

#### GET /api/claims/history — Paginated Claim History

| Field         | Value                                                                                                                                      |
|---------------|--------------------------------------------------------------------------------------------------------------------------------------------|
| **Method** | GET |
| **Path** | /api/claims/history |
| **Request Body** | None |
| **Query Params** | page, limit, status, companyName, fromDate, toDate |
| **Response Shape** | `{ "items": [{ "claimId", "companyName", "status", "submittedAt", "completedAt" }], "pagination": { "totalCount", "currentPage", "totalPages", "limit", "hasNextPage", "hasPrevPage" } }` |
| **Status Codes** | `200 OK` - Claims Successfully Retrieved<br>`400 Bad Request` - Invalid query parameters. |

#### PUT /api/claims/{id} — Update Claim

| Field        | Value                                                        |
|--------------|--------------------------------------------------------------|
| **Method** | PUT |
| **Path** | /api/claims/{id} |
| **Request Body** | `{"status": String, "NormalizedClaimData": String}` |
| **Response Shape** | None |
| **Status Codes** | `204 No Content` - Claim Successfully Updated<br>`404 Not Found` - Claim Not Found. |

## Maker Flow Endpoint

#### GET /api/maker/claims/pending — List Pending Claims

| Field        | Value                                                                              |
|--------------|------------------------------------------------------------------------------------|
| **Method** | GET |
| **Path** | /api/maker/claims/pending |
| **Header** | `X-User-Id: {makerId}` |
| **Request Body** | None |
| **Response Shape** | `[{"ClaimId": UUID, "CompanyId": UUID, "SubmittedAt": Datetime, "Status": String}]` |
| **Status Codes** | `200 OK` - Claims Successfully Retrieved<br>`403 Forbidden` - User not found or not a Maker. |

#### POST /api/maker/claims/{claimId}/accept — Accept a Claim

| Field        | Value                                                                                                      |
|--------------|------------------------------------------------------------------------------------------------------------|
| **Method** | POST |
| **Path** | /api/maker/{claimId}/accept |
| **Header** | `X-User-Id: {makerId}` |
| **Request Body** | None |
| **Response Shape** | `{"Message": String, "MakerId": UUID, "ClaimId": UUID, "Status": String, "MakerAcceptedAt": Datetime}` |
| **Status Codes** | `200 OK` - Claim Successfully Accepted<br>`400 Bad Request` - Claim is not in Pending State.<br>`403 Forbidden` - User not found or not a Maker.<br>`404 Not Found` - Claim not found |

#### GET /api/maker/{claimId}/claim — View Accepted Claim Details

| Field        | Value                                                                     |
|--------------|---------------------------------------------------------------------------|
| **Method** | GET |
| **Path** | /api/maker/{claimId}/claim |
| **Header** | `X-User-Id: {makerId}` |
| **Request Body** | None |
| **Response Shape** | `{"ClaimId": UUID, "CompanyName": String, "NormalizedClaimData": String, "Status": String}` |
| **Status Codes** | `200 OK` - Claim Successfully Retrieved<br>`403 Forbidden` - Not a maker or claim not assigned to the current maker.<br>`404 Not Found` - Claim not found |

#### POST /api/maker/{claimId}/submit — Submit Maker Review

| Field        | Value                                                                                                                   |
|--------------|-------------------------------------------------------------------------------------------------------------------------|
| **Method** | POST |
| **Path** | /api/maker/{claimId}/submit |
| **Header** | `X-User-Id: {makerId}` |
| **Request Body** | `{"makerFeedback": String, "recommendation": String}` |
| **Response Shape** | `{"Message": String, "MakerId": UUID, "ClaimId": UUID, "ReviewId": UUID, "Status": String, "MakerSubmittedAt": Datetime}` |
| **Status Codes** | `200 OK` - Claim Successfully Submitted<br>`400 Bad Request` - Claim is not in `Maker_In_Progress` State.<br>`403 Forbidden` - Not a maker or claim not assigned to the current maker.<br>`404 Not Found` - Claim not found |

## Checker Flow

#### GET /api/checker/claims/pending — List Claims Awaiting Checker

| Field        | Value                                                                                                              |
|--------------|--------------------------------------------------------------------------------------------------------------------|
| **Method** | GET |
| **Path** | /api/checker/claims/pending |
| **Header** | `X-User-Id: {checkerId}` |
| **Request Body** | None |
| **Response Shape** | `[{"ReviewId: UUID, "ClaimId": UUID, "CompanyId": UUID, "MakerRecommendation": String, "SubmittedAt": Datetime }]` |
| **Status Codes** | `200 OK` - Claims Successfully Retrieved<br>`403 Forbidden` - User not found or not a Checker. |

#### POST /api/checker/{claimId}/accept — Accept a Claim for Checker Review

| Field        | Value                                                                                                          |
|--------------|----------------------------------------------------------------------------------------------------------------|
| **Method** | POST |
| **Path** | /api/checker/{claimId}/accept |
| **Header** | `X-User-Id: {checkerId}` |
| **Request Body** | None |
| **Response Shape** | `{"Message": String, "CheckerId": UUID, "ClaimId": UUID, "Status": String, "CheckerAcceptedAt": Datetime}` |
| **Status Codes** | `200 OK` - Claim Successfully Accepted<br>`400 Bad Request` - Claim is not in `Checker_Pending` State.<br>`403 Forbidden` - User not found or not a Checker.<br>`404 Not Found` - Claim or Review not found |

#### GET /api/checker/{claimId}/claim — View Accepted Claim Details

| Field        | Value                                                                                                                                                   |
|--------------|---------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Method** | GET |
| **Path** | /api/checker/{claimId}/claim |
| **Header** | `X-User-Id: {checkerId}` |
| **Request Body** | None |
| **Response Shape** | `{"ClaimId": UUID, "CompanyName": String, "NormalizedClaimData": String, "Status": String, "MakerRecommendation": String, "MakerFeedback": String, "MakerSubmittedAt": Datetime}` |
| **Status Codes** | `200 OK` - Claim Successfully Retrieved<br>`403 Forbidden` - Not a checker or not assigned to the current checker.<br>`404 Not Found` - Claim not found |

#### POST /api/checker/{claimId}/submit — Submit Final Decision

| Field        | Value                                                                                                                             |
|--------------|-----------------------------------------------------------------------------------------------------------------------------------|
| **Method** | POST |
| **Path** | /api/checker/{claimId}/submit |
| **Header** | `X-User-Id: {checkerId}` |
| **Request Body** | `{"finalDecision": String}` |
| **Response Shape** | `{"Message": String, "CheckerId": UUID, "ClaimId": UUID, "ReviewId": UUID, "FinalDecision": String, "CheckerSubmittedAt": Datetime}` |
| **Status Codes** | `200 OK` - Claim Successfully Submitted<br>`400 Bad Reques`t - Claim is not in `Checker_In_Progress` State.<br>`403 Forbidden` - Not a checker or not assigned to the current checker.<br>`404 Not Found` - Claim not found |

## Assumptions

| ID  | Assumption                                                                                                                                                                                           |
|-----|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| A-01 | User identity is passed via the `X-User-Id` request header for distinguishing Maker and Checker.                              |
| A-02 | `RawClaimData` and `NormalizedClaimData` are stored as strings to remain format-agnostic across different insurance company formats. `NormalizedClaimData` would usually be a `JSONB` column. |
| A-03 | The upstream OCR/extraction service is assumed to have already structured the claim data before it reaches this API. InsureZen is not responsible for document parsing.                              |
| A-04 | `NormalizedClaimData` may be supplied by the caller in the submission request. In a real system, normalization would be performed server-side using company-specific logic.                          |
| A-05 | Forwarding a completed claim to the insurance company is implemented as a server-side log entry. No real external communication is performed.                                                        |
