# TasmoCC

A command center for all your Tasmota devices.



# Overview

TasmoCC is a solution for central management of [Tasmota](https://tasmota.github.io/docs/) devices. It allows you to easily configure, monitor and control hundreds of tasmota devices using a modern, responsive web interface.

This is a work in progress. I'm writing it on my spare time. While I'm using it for some time now (with 30+ devices) it is not ready for public usage. I'm publishing the code for now just to honor a friend request :)



## Features

- Modern, real-time web interface 
  - Proudly made with [Bootstrap](https://getbootstrap.com/), [React](https://reactjs.org/) and [SignalR](https://dotnet.microsoft.com/apps/aspnet/signalr)
  - MQTT based: [it will not overload your devices](https://tasmota.github.io/docs/FAQ/#tasmota-is-sending-many-status-updates-every-5-seconds) with polling
  - Inspired by Ubiquiti Networks [Unifi Controller](https://www.ui.com/software/)
- Embrace MQTT service (requires one!)
- Fully asynchronous
  - Commands to device are sent over HTTP.
  - Feedback from device are received over MQTT.
- A fast network scanner (scan an entire /24 subnet in ~15 seconds)
  - Network scans (instead of UI CRUDs) are the primary way to register new devices.
- Dark mode!



## Current limitations

- One /24 subnet only
- No UI for application configuration. You must use the config file.
- Tasmota 8.2.0 or later only. 
  - Avoids `FullTopic "%topic%/%prefix%"` madness and keeps the codebase simple.
- Minor UI glitches.



## To Do

- Write documentation
- Add dashboard / home page
- Add modal dialogs for confirmations. 
- Add toasts for notifications.
- Create docker images
- CI/CD
- More integration tests
- Finish tasmota emulator (useful for demos)



# Quick setup

- Setup a MongoDB instance with [change streams](https://www.mongodb.com/blog/post/an-introduction-to-change-streams) support 
- Configure application via `dotnet user-secrets` or environment variables

| Configuration             | Sample                                         |
| ------------------------- | ---------------------------------------------- |
| Tasmota:Subnet            | 192.168.20.0                                   |
| Tasmota:ConfigurationFile | C:\Projects\TasmoCC\Sample\tasmocc.yaml        |
| Mqtt:Host                 | 192.168.20.10                                  |
| Mqtt:Username             | tasmota                                        |
| Mqtt:Password             | secret                                         |
| MongoDb:ConnectionString  | mongodb://localhost                            |



# Development notes

## Device states

| State            | Condition                                                               |
| ---------------- | ----------------------------------------------------------------------- |
| Offline          | `offline == true`                                                       |
| AdoptionPending  | `adoptedAt == null && state != 'Adopting'`                              |
| Adopting         | `adoptedAt == null && state == 'Adopting'`                              |
| ProvisionPending | `adoptedAt != null && state != 'Provisioning'`                          |
| Provisioning     | `adoptedAt != null && state == 'Provisioning' && provisionedAt == null` |
| Connected        | `adoptedAt != null && provisionedAt != null`                            |
| Restarting       | (upon request only)                                                     |
| Upgrading        | (upon request only)                                                     |



## Workflow

| State            | On                 | Do                                                                          |
| ---------------- | ------------------ | --------------------------------------------------------------------------- |
| AdoptionPending  | Adopt request      | `state = 'Adopting'; adoptedAt = null; ConfigureMqtt()` (restart)           |
| Adopting         | Become online      | `TestMqttConfiguration()`                                                   |
| Adopting         | Mqtt test received | `adoptedAt = Now` (Adoption completed)                                      |
| Adopting         | Timeout            | `state = 'AdoptionPending'` (Adoption failed)                               |
| ProvisionPending | Provision request  | `state = 'Provisioning'; provisionedAt = null; ConfigureDevice()` (restart) |
| Provisioning     | Telemetry received | `state = null; provisionedAt = Now` (Provision completed)                   |
| Connected        | Restart request    | `state = 'Restarting'; RestartDevice()` (restart)                           |
| Restarting       | Become online      | `state = null;` (Restart completed)                                         |
| Upgrading        | Become online      | `state = null;` (Upgrade completed)                                         |
