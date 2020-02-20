# Alexa S2 Crestron

A proof-of-concept project demonstrating the control of a Crestron Series 2 processors using Amazon Alexa voice commands. 

## Requirements to Deploy/Run

- Crestron Series 2 Processor (tested on DMPS-300-C)
    - Networking configured and accessible from where the Alexa_S2 app runs application runs.
    - TCP port 41790 open (opens when an xpanel is programmed on it)
- AWS Developer Account
- C# Development enviornment
- [.NET Core 2.1](https://dotnet.microsoft.com/download/dotnet-core/thank-you/sdk-2.1.803-windows-x64-installer) installed
- Clone this repo locally

## Deployment

### AWS IoT (MQTT)
This is the MQTT Message queue used by both the Alexa S2 app and the Lambda function.

- Login to [AWS IoT Core Console](https://console.aws.amazon.com/iot/home)

- Create a new Policy
  - 'Secure'
  - 'Policies'
  - 'Create'
  - Provide a 'Name'
  - Enter:
    - Action: "iot:Publish, iot:Subscribe, iot:Connect, iot:Receive"
    - Resource ARN: "*"
  - 'Create'

- Create a 'Thing'
  - 'Manage'
  - 'Things'
  - 'Create' a new Thing
  - Give it a 'Name' (e.g. Crestron)
  - Create a new 'Type'
  - Give the Type a 'Name'
  - 'Create thing type'
  - 'Create Certificate' with One-Click certificate creation.
  - Download all 3 certs and the root CA for AWs IoT.
  - 'Activate'
  - 'Attach Policy'
  - Select the policy from above.


### Alexa S2 App (crestron-s2-alexa\Alexa S2)
This app translates AWs IoT MQTT messages into Crestron Seris 2 xpanel commands. 

Note: You must verify the xpanel button ids are correct or update them within the Program.cs source:
```
public enum CrestronButtonID
{
    volume_up = 6,
    volume_down = 7
};
```

- Build the Alexa S2 App (C# App)
```
PS >dotnet.exe build Alexa S2.csproj /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary
```

- Run the Alexa S2 App (C# App)
```
PS > & '.\Alexa S2.exe'
Connecting to MQTT Server...
AWS IoT dotnet message consumer starting..
Connected to AWS IoT with client ID: c2d4d419-bcb1-48bb-af09-bdfa5d99884b
Connecting to Crestron Processor...
Socket connected to 192.168.7.78:41790
Waiting for data from the server...
```

- Take note of the lambda ARN for later.

### Alexa Skill
This skill processes voice commands from Echo devices and invokes the above lambda function.

- Login to [Alexa Developer Console](https://developer.amazon.com/alexa/console)

- 'Create Skill'
  - Enter a skill name (e.g. 'Crestron') 
  - Select 'Custom' model
  - Select 'Provision your own' backend resources.
  - Click 'Create Skill'
  - Select 'Start from Scratch' for the template.

- Go down the Skill builder checklist
  - Enter an Invocation Name (e.g. 'Crestron Switch)
  - Add a custom intent called 'VolumeUpIntent'
    - Sample Utterances: 'increase volume', 'turn up volume', 'volume up', 'turn the volume up'
  - Add a custom intent called 'VolumeDownIntent'
    - Sample Utterances: 'decrease volume', 'turn down volume', 'volume down', 'turn the volume down'
  - Build the Model

- Take note of the skill ID for later.


### AWS Lambda (crestron-s2-alexa/Crestron lambda)
This lambda function receives Intents from the Alexa Skill below and publishes AWS IoT Messages.

- Login to [AWS Lambda Console](https://console.aws.amazon.com/lambda)

- 'Create Function'
  - 'Author from scratch'
  - Enter a function name (e.g. 'crestronControl')
  - Select '.NET Core 2.1' Runtime
  - 'Create'

- 'Add trigger'
  - Select 'Alexa Skills Kit'
  - Enter the Skill Id from the Skill noted above.

- Add the Function code
    - build/zip the lambda function
    ```
    PS src\crestronControl> dotnet lambda package -c Release -o ../CrestronLambda.zip -f netcoreapp2.1
    ```

    - Select '.Net Core 2.1' Runtime
    - Upload the zip file produced from the Build above.
    - Enter the 'Handler' in the format: assembly::namespace.class-name::method-name. (e.g. 'crestronControl::crestronControl.Function::FunctionHandler')

- Click 'Save'

- Connect Alexa Skill to Lambda
  - Return to your Crestron Alexa Skill within the [Alexa Developer Console](https://developer.amazon.com/alexa/console)
  - Add an Endpoint
    - Enter the ARN for the lambda function above in the 'Default Region' field.

## Testing the System
Once the AWS IoT Thing is created, the Alexa S2 App is built and running, the lambda function is created, and the Alexa Skill is created, it's ready to test.

- Add the Skill to Alexa by saying 'open \<crestron skill name\>' or through the [Alexa Dashboard](https://alexa.amazon.com/spa/index.html#skills/your-skills)
- Say a command:
  - 'Alexa, tell \<invocation name\> to turn the volume up'
  - 'Alexa, tell \<invocation name\> to turn the volume down'
- Verify that the volume increases/decreases.

## MQTT Message Format

Topic: *crestron*

Payload: *{
  'cmd': \<command\>*,
  'parameters': \<parameter string\>
}

### Available commands
- "volume_up" : Press and release the button used to increase the volume.
- "volume_down" : Press and release the button used to descrease the volume.
- "heartbeat" : send a heartbeat (normally not used but good for testing)
