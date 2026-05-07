# Eaat - System Arkitektur & Dokumentation

Denne dokumentation beskriver kernesystemet i Eaat-platformen, en skalerbar microservice-løsning bygget i C# .NET med RabbitMQ som besked-motor.

## Systemoversigt

Systemet består af tre uafhængige services, der kommunikerer asynkront via en **Topic Exchange** i RabbitMQ. Dette sikrer høj skalerbarhed og robusthed (fejltolerance), hvis en del af systemet midlertidigt er nede.

### Arkitektur-diagram (Mermaid)

```mermaid
graph TD
    subgraph "Messaging Layer (RabbitMQ)"
        EX[eaat_exchange - Topic]
        
        RQ[restaurant_order Queue]
        CQ[courier_queue Queue]
        OQ[order_updates Queue]
    end

    subgraph "Microservices (.NET)"
        OS[OrderService]
        RS[RestaurantService]
        CS[CourierService]
    end

    %% Flow 1: Order Placement
    OS -- "1. Publish: order.created" --> EX
    EX -- "Routing" --> RQ
    RQ -- "Consume" --> RS

    %% Flow 2: Restaurant Confirmation
    RS -- "2. Publish: order.confirmed" --> EX
    EX -- "Routing" --> OQ
    EX -- "Routing" --> CQ
    
    OQ -- "3. Notify Customer & Update DB" --> OS
    CQ -- "4. Create Delivery Offer" --> CS

    %% Flow 3: Courier Competition (First-come-first-served)
    CS -- "5. POST /accept (Courier wins)" --> CS
    CS -- "6. Publish: courier.assigned" --> EX
    CS -- "7. Broadcast: courier.broadcast.taken" --> EX
    
    EX -- "8. Notify Customer: Courier on way" --> OQ
    EX -- "9. Notify other couriers: Task taken" --> CQ
```

## Tekniske Valg & Implementering

### 1. Kommunikation (RabbitMQ)
Vi bruger en **Topic Exchange** (`eaat_exchange`), da det giver den mest fleksible routing. 
- **Direct Messaging:** Bruges når en specifik service skal modtage en besked (f.eks. `order.created` til restauranten).
- **Broadcast Messaging:** Bruges til at informere alle interesserede parter om en statusændring (f.eks. `courier.broadcast.taken`, så alle bud-instanser ved, at opgaven er væk).

### 2. Skalerbarhed & Pålidelighed
Da alle services kører i hver deres Docker-container (se `compose.yaml`), kan de skaleres uafhængigt. RabbitMQ fungerer som en buffer; hvis `RestaurantService` er nede, bliver ordrerne liggende i køen, indtil servicen er klar igen.

### 3. Først-til-mølle (Race Conditions)
Logikken for tildeling af bud ligger i `CourierService`. Ved at bruge en atomar opdatering af databasen (`IsAssigned = true`), sikrer vi, at kun det første bud, der rammer API'et, får opgaven. Alle efterfølgende forsøg får en fejlbesked.

### 4. Datakonsistens
Systemet benytter **Eventual Consistency**. I stedet for én stor låst transaktion på tværs af services, sendes beskeder der opdaterer de respektive systemer asynkront. Dette er en "best practice" inden for microservices for at undgå flaskehalse.

## Sådan køres systemet
1. Start infrastrukturen: `docker-compose up -d` (Starter RabbitMQ).
2. Start de tre services (`OrderService`, `RestaurantService`, `CourierService`).
3. Brug Scalar/Swagger på de respektive porte for at teste endpoints.
