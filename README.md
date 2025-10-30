# 🚀 HackerNews Top Stories API

This is a robust, high-performance ASP.NET Core API designed to retrieve and serve the top stories from the Hacker News public API. It implements several architectural and performance best practices, including **caching**, **rate limiting**, and **resilience policies** (Retry, Circuit Breaker, Timeout) via Polly.

## 🌟 Features

* **High Performance Caching:** Uses **Redis** as a distributed cache to store the latest top 200 stories, ensuring millisecond response times for API consumers.
* **Automatic Cache Warmup:** A dedicated `CacheWarmupHostedService` runs in the background to automatically refresh the cache data every **2 minutes**, ensuring the cache is always fresh.
* **Resilience (Polly):** Implements advanced policies for handling external API instability:
    * **Retry Policy:** Automatically retries transient HTTP errors (5xx) and `429 Too Many Requests`.
    * **Circuit Breaker:** Opens the circuit after 5 consecutive failures for 30 seconds to protect the upstream service (Hacker News API) from being overwhelmed.
    * **Timeout Policy:** Cancels long-running requests (over 8 seconds).
* **Authentication (JWT):** Secures the main data endpoints using **JWT Bearer** token authentication.
* **Rate Limiting:** Implements a Fixed Window Rate Limiter (60 requests/minute per authenticated client) to protect the API resources.
* **Dockerized:** Fully containerized and ready to run with `docker-compose`.

---



## ⚙️ How to Run Locally (Recommended: Docker)

The application is configured to run easily using Docker and Docker Compose, which sets up the necessary **ASP.NET Core API**, **Redis cache**, and **SQLite database** (for users).

### Prerequisites

* Docker and Docker Compose installed.

### Steps

1.  **Set Environment Variables:**
    Create a file named `.env` in the root directory (where `docker-compose.yml` is located) and define the essential JWT settings.

    ```bash
    # .env file
    JWT_KEY=Your_Super_Secret_Key_That_Should_Be_Longer_Than_256Bits
    JWT_ISSUER=HackerNewsTopApi
    JWT_AUDIENCE=HackerNewsAppUsers
    ```

2.  **Build and Run the Containers:**
    Execute the following command in the root directory:

    ```bash
    docker-compose up --build
    ```

3.  **Access the API:**
    * **API Base URL (HTTP):** `http://localhost:5000`
    * **Swagger UI:** `http://localhost:5000/swagger`

---



## 🔑 Authentication and Endpoints

### Authentication Endpoints (`/api/Auth`)

To access the protected stories endpoint, you must first obtain a token via the `Auth` controller.

| Method | Path | Description | Example Body |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/Auth/register` | Creates a new user. | `{"username": "user", "password": "password"}` |
| `POST` | `/api/Auth/login` | Logs in and returns a JWT. | `{"username": "user", "password": "password"}` |

### Main Endpoints (`/api/Stories`)

| Method | Path | Security | Description |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/Stories?count=N` | **[Authorize], [RateLimited]** | Retrieves the top `N` stories from the cache (max 200). |
| `POST` | `/api/Stories/warmup` | **[Authorize]** | Manually triggers the cache warming process for development/testing. |

---



## 🧠 Architectural Decisions and Rationale

The design emphasizes **performance**, **stability**, and **decoupling**.

| Component | Files | Decision Rationale |
| :--- | :--- | :--- |
| **Caching** | `HackerNewsService.cs`, `CacheWarmupHostedService.cs` | **Pre-fetching and Caching:** The Hacker News API is slow. By fetching and processing the data *every 2 minutes* in the background (`CacheWarmupHostedService`) and storing it in **Redis** (`IDistributedCache`), we shift the load from the user request time to the background process. This is crucial for user-facing latency. |
| **Resilience** | `PollyConfiguration.cs`, `Program.cs` | **Polly Policy Wrap:** Wrapping the `HttpClient` with a `Timeout` $\to$ `Circuit Breaker` $\to$ `Retry` strategy is essential. It prevents **cascading failures**, handles transient network issues gracefully (Retry), and protects both our service and the upstream HN API from overload (Circuit Breaker). |
| **Authentication** | `AuthService.cs`, `AuthController.cs` | **Decoupling and Security:** Separated authentication logic (`AuthService`) from the controller and used **BCrypt** for secure password hashing. Used standard **JWT Bearer** for stateless, scalable authentication. |
| **Rate Limiting** | `StoriesController.cs`, `Program.cs` | **Fixed Window Limiting:** Applied a `60 requests/minute` limit to the core data endpoint (`/api/Stories`) to ensure fair usage and protect API resources. |
| **Parallelism** | `HackerNewsService.cs` | **Throttled Parallel Requests:** Used a `SemaphoreSlim(10)` to fetch individual story details concurrently but limited the number of simultaneous HTTP requests to the external API, balancing speed with respect for the upstream service's capacity. |

```
| Request                       | Calls to HN                     | Time   |
|-------------------------------|---------------------------------|--------|
| GET /stories?count=10         | **0** (cache)                   | ~50ms  |
| GET /stories?count=50         | **0** (cache)                   | ~50ms  |
| GET /stories?count=200        | **0** (cache)                   | ~100ms |
| Background warmup (each 2min) | 1 (IDs) + 200 (itens) = **201** | ~8s    |


┌─────────────────┐
│   Client       │
└────────┬────────┘
         │ GET /stories?count=10
         │ Latência: ~50ms
         ▼
┌─────────────────────────┐
│  StoriesController      │
└────────┬────────────────┘
         │
         ▼
┌─────────────────────────┐     ┌──────────────────┐
│  HackerNewsService      │────▶│  Redis Cache     │
│  - GetTopStoriesAsync() │     │  (preprocessed)  │
└─────────────────────────┘     └──────────────────┘
         ▲
         │ A cada 2 minutos
         │
┌─────────────────────────┐
│ CacheWarmupHostedService│
│  - WarmupCacheAsync()   │
└────────┬────────────────┘
         │ 200 calls HTTP
         ▼
┌─────────────────────────┐
│  HackerNews API         │
│  (Firebase)             │
└─────────────────────────┘
```

---



## ✨ Potential Improvements and Future Work

These items represent key improvements that would enhance reliability, maintainability, and user experience if development time allowed:

1.  **Comprehensive Health Checks:**
    * **Improvement:** Implement detailed ASP.NET Core Health Checks, exposing an endpoint (`/health`) that verifies the connection status of **Redis**, the **SQLite database**, and the **HackerNews external API**.
    * **Why:** Allows for better monitoring and faster detection of infrastructure failures.

2.  **JWT Refresh Tokens:**
    * **Improvement:** Implement a refresh token mechanism. Currently, the JWT expires in 60 minutes.
    * **Why:** Improves user experience by allowing secure, prolonged sessions without requiring the user to re-login after token expiration.

3.  **Centralized Logging and Telemetry:**
    * **Improvement:** Integrate with a structured logging provider (e.g., Serilog) and potentially an APM tool.
    * **Why:** Provides better visibility into errors, performance bottlenecks, and the behavior of the Polly policies and cache mechanisms in production.

4.  **Optimized Story Caching (Distributed Lock):**
    * **Improvement:** Implement a **Distributed Lock** (e.g., using RedLock) during the `WarmupCacheAsync` operation.
    * **Why:** Prevents multiple instances of the API (in a scale-out scenario) from simultaneously attempting to warm up the cache when they start up or at the same interval, reducing unnecessary load on the external HN API.

5.  **Configuration via `appsettings`:**
    * **Improvement:** Move hardcoded constants like `MaxStoriesToCache` (200), `CacheDurationMinutes` (2), and `WarmupIntervalMinutes` (2) from `HackerNewsService.cs` to the `appsettings.json` file.
    * **Why:** Simplifies configuration management and allows runtime tuning of the application without rebuilding the code.
