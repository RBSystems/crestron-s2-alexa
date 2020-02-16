# Alexa S2 Crestron

A proof-of-concept project demonstrating the control of a Crestron Series 2 processors using Amazon Alexa voice commands. 

## Requirements to Run

- Crestron Series 2 Processor (tested on DMPS-300-C)
    - Networking configured and accessible from where this application runs.
    - TCP port 41790 open (opens when an xpanel is programmed on it)

## Development Requirements

- MQTTNet is required for MQTT message bus support and can be added using the dotnet package manager:
    - dotnet add '.\Alexa S2\Alexa S2.csproj' package MQTTnet --version 3.0.8
