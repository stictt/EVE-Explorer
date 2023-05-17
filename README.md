# EVE Explorer
<div id="header" align="center">
  <img src="https://upload.wikimedia.org/wikipedia/commons/thumb/5/51/EVE_online_logo.svg/1200px-EVE_online_logo.svg.png" width="650"/>
</div>

<div id="header" align="center">
  Project for a set of tools for trading, transportation, crafting and game research EVE Online 
</div>

# Context: 
<div>
  <img src="https://github.com/devicons/devicon/blob/master/icons/dot-net/dot-net-plain-wordmark.svg" title=".net 7" alt=".net 7" width="40" height="40"/>&nbsp;
  <img src="https://github.com/devicons/devicon/blob/master/icons/docker/docker-original-wordmark.svg" title="Docker" alt="Docker" width="40" height="40"/>&nbsp;
  <img src="https://github.com/devicons/devicon/blob/master/icons/git/git-original-wordmark.svg" title="git" alt="git" width="40" height="40"/>&nbsp;  
  <img src="https://github.com/devicons/devicon/blob/master/icons/postgresql/postgresql-original-wordmark.svg" title="PostgreSQL" alt="PostgreSQL" width="40" height="40"/>&nbsp;
  <img src="https://github.com/devicons/devicon/blob/master/icons/visualstudio/visualstudio-plain-wordmark.svg" title="VS" alt="VS" width="40" height="40"/>&nbsp;
  <img src="https://grpc.io/img/logos/grpc-icon-color.png" title="grpc" alt="grpc" width="40" height="40"/>&nbsp;
  <img src="https://upload.wikimedia.org/wikipedia/commons/thumb/7/71/RabbitMQ_logo.svg/2560px-RabbitMQ_logo.svg.png" title="RabbitMQ" alt="RabbitMQ" width="60" height="60"/>&nbsp;
  <img src="https://somostechies.com/content/images/2019/08/ASP-NET-Core-Logo-1.png" title="ASP-NET-Core" alt="ASP-NET-Core" width="40" height="40"/>&nbsp;
</div>

Microservices planned 

* AggregatorMicroservice 
* AnalyticalMicroservice 
* TelegramBotMicroservice 
* VirtualExchangeMicroservice

```mermaid
stateDiagram-v2
    AggregatorMicroservice --> RabbitMQ
    AnalyticalMicroservice --> RabbitMQ
    AggregatorMicroservice --> AnalyticalMicroservice
    TelegramBotMicroservice --> RabbitMQ
    AnalyticalMicroservice --> TelegramBotMicroservice
    AggregatorMicroservice --> TelegramBotMicroservice
    VirtualExchangeMicroservice --> RabbitMQ
    VirtualExchangeMicroservice --> TelegramBotMicroservice
    TelegramBotMicroservice --> User
```
# AggregatorMicroservice

the aggregator is responsible for the accumulation of primary data, which are made in the context of 5 minutes.
This allows you to get a detailed report on the change in the positions of market orders within one day.
Provides different raw data to other microservices

# AnalyticalMicroservice

Planned functionality

* Arbitrage trading module
* Depth of Market module
* Price change module relative to the timestamp
* Module of bulls and bears, the ratio of trading volumes.
* Craft analytics
* Carrier destruction statistics with this cargo
* Period Trend Formation Module

# TelegramBotMicroservice 

The bot provides user interaction functionality, and provides all the features of the following microservices
* AnalyticalMicroservice
* VirtualExchangeMicroservice

# VirtualExchangeMicroservice

Trade microservice.
A virtual exchange is planned that will allow trading on the real market of the game, goods that are available in the game.
In single player mode or competing with other players.
In order to learn how to trade without risks in the game market EVE ONLINE
