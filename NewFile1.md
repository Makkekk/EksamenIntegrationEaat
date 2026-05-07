# Eaat - System Arkitektur & Dokumentation

Denne dokumentation beskriver kernesystemet i Eaat-platformen, en skalerbar microservice-løsning bygget i C# .NET med RabbitMQ som besked-motor.

## Systemoversigt

Systemet består af tre uafhængige services, der kommunikerer asynkront via en **Topic Exchange** i RabbitMQ. Dette sikrer høj skalerbarhed og robusthed (fejltolerance), hvis en del af systemet midlertidigt er nede.

### Arkitektur-diagram (Flow & Integration)

```mermaid
graph TD
    %% Node Styles
    classDef service fill:#2d3436,stroke:#00cec9,stroke-width:2px,color:#fff
    classDef rabbit fill:#636e72,stroke:#fdcb6e,stroke-width:2px,color:#fff
    classDef actor fill:#dfe6e9,stroke:#2d3436,stroke-width:2px,color:#2d3436
    classDef db fill:#55efc4,stroke:#00b894,stroke-width:2px,color:#2d3436

    %% External Actors
    Customer((Sulten Kunde)):::actor
    Couriers((Alle Bude)):::actor

    subgraph Services [Logiske Services]
        OS[OrderService]:::service
        RS[RestaurantService]:::service
        CS[CourierService]:::service
    end

    subgraph Databases [Persistens]
        DB1[(Order DB)]:::db
        DB2[(Courier DB)]:::db
    end

    subgraph RabbitMQ [Message Broker]
        EX{eaat_exchange <br/> Topic Exchange}:::rabbit
        RQ[[restaurant_order <br/> Queue]]:::rabbit
        OQ[[order_updates <br/> Queue]]:::rabbit
        CQ[[courier_queue <br/> Queue]]:::rabbit
    end

    %% Database Connections
    OS --- DB1
    CS --- DB2

    %% Flow 1: Kunden bestiller
    Customer -- "1. HTTP POST /orders" --> OS
    OS -. "2. Publish: order.created" .-> EX
    EX -- "Routing: order.created" --> RQ
    RQ -- "Consume" --> RS

    %% Flow 2: Restauranten bekræfter
    RS -. "3. Publish: order.confirmed" .-> EX
    EX -- "Routing: order.confirmed" --> OQ
    EX -- "Routing: order.confirmed" --> CQ

    %% Flow 3: Notificering & Udbud
    OQ -- "4. Update Status & Notify" --> OS
    OS -- "Push: 'Maden er på vej'" --> Customer
    
    CQ -- "5. Persist Offer" --> CS
    CS -- "6. GET /offers" --> Couriers

    %% Flow 4: Først-til-mølle accept
    Couriers -- "7. POST /accept (Først-til-mølle)" --> CS
    CS -. "8. Publish: courier.assigned" .-> EX
    CS -. "9. Publish: courier.broadcast.taken" .-> EX
    
    EX -- "Routing: courier.assigned" --> OQ
    EX -- "Routing: courier.broadcast.taken" --> CQ
    
    OQ -- "10. Update Status" --> OS
    OS -- "Push: 'Bud er fundet'" --> Customer
```

## Tekniske Valg & Implementering

### 1. Kommunikation (RabbitMQ)
Vi bruger en **Topic Exchange** (`eaat_exchange`), da det giver den mest fleksible routing. 
- **Direct Messaging:** Bruges når en specifik service skal modtage en besked (f.eks. `order.created` til restauranten).
- **Broadcast Messaging:** Bruges til at informere alle interesserede parter om en statusændring (f.eks. `courier.broadcast.taken`, så alle bud-instanser ved, at opgaven er væk).

### 2. Databases & Persistens
Hver service har sin egen database-instans (Database per Service mønstret). 
- **Order DB:** Holder styr på ordrens tilstand og kundedata.
- **Courier DB:** Holder styr på ledige leveringsopgaver og hvem der er tildelt hvilken opgave.
Dette sikrer, at services kan køre og skalere uafhængigt af hinanden.

### 3. Skalerbarhed & Pålidelighed
Da alle services kører i hver deres Docker-container (se `compose.yaml`), kan de skaleres uafhængigt. RabbitMQ fungerer som en buffer; hvis `RestaurantService` er nede, bliver ordrerne liggende i køen, indtil servicen er klar igen.

### 4. Først-til-mølle (Race Conditions)
Logikken for tildeling af bud ligger i `CourierService`. Ved at bruge en atomar opdatering af databasen (`IsAssigned = true`), sikrer vi, at kun det første bud, der rammer API'et, får opgaven. Alle efterfølgende forsøg får en fejlbesked.

### 5. Datakonsistens
Systemet benytter **Eventual Consistency**. I stedet for én stor låst transaktion på tværs af services, sendes beskeder der opdaterer de respektive systemer asynkront. Dette er en "best practice" inden for microservices for at undgå flaskehalse.

### 6. Overvejelser om en separat NotificationService
I denne prototype håndteres notificering af kunden direkte i `OrderService`. I et større produktionsmiljø ville man med fordel kunne udskille dette i en dedikeret **NotificationService**.

## Sådan køres systemet
1. Start infrastrukturen: `docker-compose up -d` (Starter RabbitMQ).
2. Start de tre services (`OrderService`, `RestaurantService`, `CourierService`).
3. Brug Scalar/Swagger på de respektive porte for at teste endpoints.
