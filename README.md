# OmniConnect Smart Home Network Simulator

## Project Overview
OmniConnect is a distributed smart home simulation system that demonstrates the use of TCP and UDP socket programming.  
The system models a real smart home with sensors, actuators, and user interfaces communicating through a central Hub.

The Hub acts as the server, managing all devices and system state.  
Clients connect to the Hub using TCP for reliable commands and events, and UDP for real-time state broadcasting.

This project was developed as a final project for the Computer Network Programming course.

---

## System Components

### Server
**Hub**
- Central server
- Accepts TCP connections from all devices
- Processes sensor data
- Controls actuators
- Broadcasts system state via UDP

### Clients

**Sensors**
- ThermoSensor
- MotionSensor

**Actuators**
- SmartLamp
- SmartLock

**User Interfaces**
- ControlPanel
- SecurityCenter

**Support Service**
- LogController (Named Pipe based logging service)

**Shared Library**
- Common (shared message models and utilities)

---

## Network Communication Design

| Purpose | Protocol | Reason |
|--------|--------|--------|
| Device registration and commands | TCP | Reliability and ordering |
| Motion events and file transfer | TCP | Guaranteed delivery |
| Temperature telemetry | UDP | Low latency |
| Real-time system state | UDP | Fast broadcast |
| Local logging | Named Pipes | High performance IPC |

---

## How to Build

1. Open `OmniConnect.slnx`
2. Build the solution
3. Make sure all projects compile successfully

---

## How to Run

1. Start **Hub** (Server)
2. Start the following clients:
   - ThermoSensor
   - MotionSensor
   - SmartLamp
   - SmartLock
   - ControlPanel
   - SecurityCenter
   - LogController

Clients can be started in any order after the Hub.

---

## Features

- Multi-client TCP server
- UDP real-time state broadcasting
- Motion-triggered events with file transfer
- Rule-based automation system
- Live control panel
- Security alert visualization
- Named pipe based logging

---

## Testing

The system was tested using:
- Multiple simultaneous clients
- Frequent UDP sensor updates
- Motion events and snapshot transfers
- Dynamic rule updates
- Client disconnections

---

## Credits

Developed by: Elif Nida Solakoglu  
AI Tools Used: ChatGPT, Claude, and Gemini  
AI tools were used for architecture planning, debugging, protocol design, and visual asset generation.
