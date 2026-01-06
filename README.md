🚀 OmniConnect – Smart Home Network Simulator

OmniConnect is a distributed smart home simulation platform developed as a Computer Network Programming project.
It demonstrates real-world usage of TCP, UDP, and Inter-Process Communication (IPC) in a modular IoT-style architecture.

The system models a real smart home where multiple independent devices (sensors, actuators, and user interfaces) communicate through a central Hub.

🧠 Architecture Overview

OmniConnect follows a hub-and-spoke architecture:

ThermoSensor  ──┐
MotionSensor ──┤
SmartLamp    ──┤
SmartLock    ──┤──>   HUB   ───> ControlPanel
LogController──┘             └──> SecurityCenter


All components are implemented as independent processes.

The Hub is responsible for:

Device registration

Message routing

Rule evaluation

State broadcasting

Alert generation

🌐 Network & IPC Design
Purpose	Technology	Why
Device registration & commands	TCP	Reliable and ordered
Motion events & file transfer	TCP	Guaranteed delivery
Temperature telemetry	UDP	Low latency
System state broadcast	UDP	Real-time updates
Logging	Named Pipes (IPC)	Fast local communication

This hybrid design mirrors how real IoT systems separate control traffic from telemetry.

🔥 Rule Engine

OmniConnect includes a dynamic Rule Engine powered by a simple DSL:

IF <conditions> THEN <actions>


Example:

IF MOTION=ON AND MODE=AWAY THEN LOCK=LOCK; ALERT=WARNING:ROOM
IF TEMP>28 THEN LAMP=ON


Supported conditions:

Temperature thresholds

Motion detection

System mode (HOME / AWAY)

Room information

Motion frequency

Actions:

Turn lamp on/off

Lock/unlock doors

Trigger INFO / WARNING / CRITICAL alerts

Rules can be changed without restarting the system.

🧩 System Components
Server

Hub
Central TCP/UDP server, rule engine, and state manager.

Sensors

ThermoSensor (UDP temperature telemetry)

MotionSensor (TCP motion + snapshot transfer)

Actuators

SmartLamp

SmartLock

User Interfaces

ControlPanel – live system state

SecurityCenter – visual alert map

Support

LogController – Named Pipe based logging

Common – shared message models & configs

▶️ How to Run

Open OmniConnect.slnx

Build the solution

Start components in this order:

Hub
ThermoSensor
MotionSensor
SmartLamp
SmartLock
ControlPanel
SecurityCenter
LogController


Clients may be started in any order after the Hub.

🧪 Features Demonstrated

Multi-client TCP server

UDP telemetry & broadcasting

File transfer over TCP

Rule-based automation

Live UI dashboards

Inter-process logging

Fault-tolerant design

🎯 Why This Project Is Strong

OmniConnect is not just a “socket demo” — it models:

A real IoT architecture

Hybrid protocol usage

Event-driven automation

Concurrent distributed systems

It demonstrates how different communication models are combined in a realistic networked system.

👤 Author

Elif Nida Solakoğlu
Computer Engineering
