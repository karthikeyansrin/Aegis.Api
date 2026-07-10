# 🛡️ Aegis

> AI-powered Scam Detection, Intelligence Extraction & Autonomous Honeypot Platform.

Aegis is an AI-native security platform that detects scam messages, extracts actionable intelligence, and can autonomously engage malicious actors using human-like AI conversations.

Unlike traditional rule-based spam filters, Aegis combines LLM reasoning, structured entity extraction, conversation memory, and deception strategies to help applications identify and respond to modern scams.

---

# Why Aegis?

Modern scams evolve faster than static detection rules.

Aegis provides a plug-and-play API that allows applications to:

- Detect scams in real time
- Extract threat intelligence
- Maintain multi-turn conversations
- Generate believable AI responses
- Build reusable scam intelligence

The long-term vision is to become the **AI Trust Layer** that sits between communication platforms and their users.

---

# Current Features

## AI Scam Detection

Uses an LLM to classify incoming messages into:

- Scam / Not Scam
- Scam category
- Confidence score

Example:

```json
{
  "isScam": true,
  "scamType": "phishing",
  "confidence": 0.98
}
```

---

## Intelligence Extraction

Automatically extracts structured intelligence from scam conversations.

Currently supported:

- UPI IDs
- Phone Numbers
- URLs
- Bank Account Numbers
- IFSC Codes

Regex is used for fast extraction.

If structured entities cannot be identified confidently, an LLM fallback is used.

---

## Autonomous Honeypot Replies

Generates realistic human-like responses that:

- keep scammers engaged
- avoid exposing the system
- never leak sensitive information
- never request or reveal credentials
- maintain short conversational replies

Example:

> "That's strange... why exactly do you need me to transfer money first?"

---

## Conversation Memory

Aegis maintains session state across requests.

This allows:

- multi-turn conversations
- previous intelligence reuse
- context-aware replies
- cumulative entity extraction

---

## Secure REST API

Single endpoint for scam analysis.

```
POST /api/aegis/analyze
```

Protected using API Key authentication.

---

## Production Deployment

Currently deployed on Railway.

The service exposes:

- Analysis endpoint
- Health endpoint
- OpenAPI / Swagger

---

# Architecture

```
                Incoming Message
                       │
                       ▼
               Scam Detection
                       │
             ┌─────────┴─────────┐
             ▼                   ▼
      Intelligence          Conversation
       Extraction              Memory
             │                   │
             └─────────┬─────────┘
                       ▼
             Honeypot AI Agent
                       │
                       ▼
                Structured Response
```

---

# Technology Stack

Backend

- ASP.NET Core (.NET 8)
- C#

AI

- OpenAI Compatible API
- Structured Prompt Engineering

Deployment

- Railway

Authentication

- API Key Middleware

---

# Example Response

```json
{
  "isScam": true,
  "scamType": "phishing",
  "confidence": 0.97,
  "agentReply": "I'm not sure why I need to do that. Can you explain a bit more?",
  "extractedIntelligence": {
    "upiIds": [
      "fraud@upi"
    ],
    "phoneNumbers": [
      "9876543210"
    ],
    "urls": [],
    "bankAccounts": [
      {
        "accountNumber": "123456789012",
        "ifsc": "HDFC0001234"
      }
    ]
  }
}
```

---

# Roadmap

## Phase 1 (Completed)

- AI scam detection
- Intelligence extraction
- Honeypot responses
- Conversation memory
- Railway deployment
- REST API
- API authentication

---

## Phase 2

- Modern React dashboard
- Conversation explorer
- Intelligence viewer
- Threat timeline
- Swagger improvements
- Docker support

---

## Phase 3

Threat Intelligence Engine

- IP reputation
- URL reputation
- Domain reputation
- Email reputation
- Threat scoring

---

## Phase 4

Enterprise Security

- Rate limiting
- Redis caching
- PostgreSQL
- Structured logging
- OpenTelemetry
- Alerting

---

## Phase 5

AI Trust Platform

- MCP Server
- SDKs
- Multi-tenant SaaS
- Policy Engine
- Plugin Marketplace

---

# Long-Term Vision

Aegis is evolving beyond a scam detection API.

The goal is to become an **AI-native Trust Platform** that applications and AI agents can use before acting on untrusted communication.

Instead of every application implementing its own security logic, applications can simply ask Aegis:

> "Should I trust this?"

and receive a structured decision with confidence, reasoning, and recommended actions.

---

# Future Integrations

- WhatsApp
- Banking Apps
- Email Providers
- Marketplaces
- Dating Apps
- Customer Support Platforms
- AI Assistants
- MCP Clients
- Browser Extensions

---

# License

MIT License