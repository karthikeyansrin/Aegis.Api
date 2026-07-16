# 🛡️ Aegis

> AI-native Trust Infrastructure for detecting, analyzing, and safely engaging scam conversations.

Aegis is an AI-powered security platform that helps applications identify scam messages before users interact with them.

Instead of relying only on static rules or keyword matching, Aegis combines Large Language Models (LLMs), conversation memory, intelligence extraction, and autonomous honeypot responses to detect modern conversational scams.

Its long-term vision is to become the **AI Trust Layer** between communication platforms and their users—allowing applications to ask a single question before proceeding:

> **"Should this interaction be trusted?"**

---

# Problem

Modern scams have evolved beyond simple spam messages.

Attackers now use:

- Multi-turn conversations
- Social engineering
- UPI fraud
- Fake customer support
- Investment scams
- Payment redirection
- Phishing links

Traditional spam filters focus on keywords and blacklists, making them ineffective against conversational scams.

Applications today have no reusable trust layer capable of understanding the intent behind a conversation.

---

# Solution

Aegis provides a plug-and-play REST API that enables any application to:

- Detect scam intent using AI
- Extract structured threat intelligence
- Maintain conversation context
- Generate believable honeypot replies
- Return a structured trust decision

Instead of every application implementing its own fraud detection logic, applications can integrate Aegis as a centralized trust service.

---

# Demo

### API Demo

*(Add GIF or video here)*

Example workflow:

```
Incoming Message
        │
        ▼
AI Scam Detection
        │
        ▼
Threat Intelligence Extraction
        │
        ▼
Conversation Memory
        │
        ▼
Autonomous Honeypot Reply
        │
        ▼
Structured JSON Response
```

---

# Features

## 🤖 AI Scam Detection

Uses an LLM to classify conversations into:

- Scam / Not Scam
- Scam category
- Confidence score

Example:

```json
{
  "isScam": true,
  "scamType": "Phishing",
  "confidence": 0.98
}
```

---

## 🔍 Intelligence Extraction

Automatically extracts structured entities from conversations.

Currently supported:

- UPI IDs
- Phone Numbers
- URLs
- Bank Account Numbers
- IFSC Codes

Regex-based extraction is used for speed, with an LLM fallback for unstructured content.

---

## 💬 Autonomous Honeypot Agent

Generates realistic human-like responses that:

- Keep scammers engaged
- Avoid revealing system identity
- Never leak sensitive information
- Never request credentials
- Continue conversations naturally

Example:

> "That's interesting... why exactly do you need me to transfer money first?"

---

## 🧠 Conversation Memory

Maintains state across multiple requests.

Supports:

- Multi-turn conversations
- Context-aware responses
- Session history
- Incremental intelligence extraction

---

## 🔐 Secure REST API

Single endpoint:

```
POST /api/aegis/analyze
```

Protected using API Key authentication.

---

## 🚀 Production Deployment

Currently deployed on Railway.

Exposes:

- Analysis endpoint
- Health endpoint
- Swagger / OpenAPI

---

# AI Pipeline

```
Incoming Message
        │
        ▼
LLM Scam Detection
        │
        ▼
Structured Intelligence Extraction
        │
        ▼
Conversation Memory Update
        │
        ▼
Autonomous Honeypot Response
        │
        ▼
Structured JSON Response
```

---

# Architecture

```
                   Client Application
                           │
                           ▼
                 REST API Endpoint
                           │
                           ▼
                Scam Detection Service
                           │
          ┌────────────────┴───────────────┐
          ▼                                ▼
 Intelligence Extraction          Conversation Store
          │                                │
          └──────────────┬─────────────────┘
                         ▼
                Honeypot Agent Service
                         │
                         ▼
                  JSON API Response
```

---

# Tech Stack

## Backend

- ASP.NET Core (.NET 8)
- C#

## AI

- OpenAI-compatible APIs
- Prompt Engineering
- Structured JSON Outputs

## Deployment

- Railway

## Authentication

- API Key Middleware

---

# Getting Started

Clone the repository.

```bash
git clone https://github.com/<username>/aegis.git

cd aegis
```

Restore packages.

```bash
dotnet restore
```

Run the API.

```bash
dotnet run
```

The service will be available at:

```
http://localhost:5000
```

Swagger:

```
http://localhost:5000/swagger
```

---

# Environment Variables

| Variable | Description |
|-----------|-------------|
| `OPENAI_API_KEY` | OpenAI API Key |
| `OPENAI_MODEL` | Model name (optional) |
| `AEGIS_API_KEY` | API authentication key |
| `OPENAI_BASE_URL` | Optional OpenAI-compatible endpoint |

Example:

```powershell
$env:OPENAI_API_KEY="your-api-key"
$env:AEGIS_API_KEY="dev-secret-key"
```

---

# Project Structure

```
Aegis
│
├── Controllers
│   └── HoneypotController
│
├── Middleware
│   └── ApiKeyAuthMiddleware
│
├── Models
│
├── Services
│   ├── ScamDetectionService
│   ├── IntelligenceExtractionService
│   ├── HoneypotAgentService
│   ├── ConversationStore
│   └── OpenAIService
│
├── Program.cs
│
└── README.md
```

---

# Roadmap

## ✅ Phase 1 — Core Platform

- AI Scam Detection
- Intelligence Extraction
- Honeypot Responses
- Conversation Memory
- Railway Deployment
- Secure REST API

---

## 🚧 Phase 2 — Developer Experience

- React Dashboard
- Conversation Explorer
- Threat Timeline
- Docker Support
- Improved Swagger
- Better Analytics

---

## 🚧 Phase 3 — Threat Intelligence

- IP Reputation
- URL Reputation
- Domain Reputation
- Threat Scoring
- IOC Database
- Threat Intelligence Dashboard

---

## 🚧 Phase 4 — Enterprise Platform

- PostgreSQL
- Redis
- OpenTelemetry
- Structured Logging
- Rate Limiting
- Alerting
- Background Workers

---

## 🚧 Phase 5 — AI Trust Platform

- MCP Server
- SDKs (.NET, Node.js, Python)
- Multi-Tenant SaaS
- Policy Engine
- Developer Portal
- Marketplace Integrations

---

# License

This project was developed as a hackathon and portfolio project. All rights reserved.

Please contact the author for licensing or commercial use inquiries.
