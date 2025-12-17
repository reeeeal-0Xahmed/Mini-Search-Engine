#  Mini Search Engine (.NET)

A lightweight search engine built with **C# and .NET** to demonstrate how real-world search engines work internally:
crawling, indexing, ranking, and querying.

This project is designed as a **backend-focused system** with clear separation of concerns and production-style architecture.

---

##  Features

- Offline web crawler
- HTML parsing (Title, Snippet, Favicon)
- NDJSON storage format
- Inverted Index
- Prefix & partial word search
- Basic ranking algorithm
- RESTful HTTPS API
- Frontend-ready (JSON responses)

---

## 🧱 Project Structure

SearchEngine
│
├── SearchEngine.Crawler # Offline crawler (not deployed)
├── SearchEngine.Core # Search engine logic (library)
└── SearchEngine.Api # REST API (deployed)

yaml
Copy code

---

##  Architecture Overview

Crawler (offline)
↓
index.ndjson
↓
API Startup
↓
Inverted Index (in-memory)
↓
Query Engine
↓
HTTPS GET /search?q=keyword

yaml
Copy code

---

##  Project Roles Explained

### 1️ SearchEngine.Crawler (Offline Tool)
- Crawls public websites
- Extracts:
  - URL
  - Title
  - Snippet
  - Favicon
- Writes data to `index.ndjson`
- **Not deployed**

---

### 2️ SearchEngine.Core (Library)
Contains the core search logic:
- Tokenizer
- Inverted Index
- Indexer
- Query Engine
- Ranking logic

This project has **no HTTP** and is used internally by both the crawler and the API.

---

### 3️ SearchEngine.Api (Deployed Service)
- Loads `index.ndjson` at startup
- Builds the search index once
- Exposes HTTPS search endpoints
- Returns ranked results as JSON

---

##  Tech Stack

- C# / .NET
- ASP.NET Core Web API
- AngleSharp (HTML parsing)
- NDJSON
- In-memory data structures

---

##  API Usage

### Search Endpoint

GET /search?q=keyword

shell
Copy code

### Example

https://localhost:7213/search?q=geek

bash
Copy code

### Sample Response

```json
[
  {
    "url": "https://www.geeksforgeeks.org/",
    "title": "GeeksforGeeks | Your All-in-One Learning Portal",
    "snippet": "Interested in advertising with us?",
    "score": 12.5
  }
]
```
🔐 CORS Support
CORS is enabled to allow frontend applications (React, HTML, etc.) to communicate with the API.

⚠️ Notes & Limitations
Crawling is done offline

Indexing happens only at API startup

Search is fully in-memory

This is a prototype, not a Google-scale system

🧪 How to Run Locally
Run the crawler to generate index.ndjson

Copy index.ndjson to:

bash
Copy code
SearchEngine.Api/Data/index.ndjson
Run the API:

bash
Copy code
dotnet run


 Why This Design?
Separation of concerns

Fast search queries

Easy to extend

Production-inspired architecture

👤 Author
Ahmed
Computer Science student & Backend Engineer
