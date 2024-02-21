# Initial Hardware and Project Configurations

## Meadow CLI Website  
[Meadow CLI](https://developer.wildernesslabs.co/Meadow/Meadow_Basics/Meadow_CLI/)

## First Time Setup Meadow
Firstly to setup the meadow, you have to found the device port and configure the route to that port  
`meadow port list`  
`meadow config route <port>`

To get device information about hardware and firmware  
`meadow device info`

## VS Settings

### Install .NET Multiplatform App UI development in VS  
Visual Studio Installer > Modify > Workloads >  .NET Multiplatform App UI development

### Install meadow extensions  
Extensions > Manage Extensions > Online > Search "meadow" > VS 2022 Tools for Meadow > Install

### Uncheck automatic updates  
Tools > Options > Environment > Extensions > Uncheck automatically check for updates

## Project Settings

### Create a Meadow Project  
File > New > Project > Search by filter "Meadow" > F7 Feather App(C#)

### Set Meadow Device Port in the project
Double click on toolbar and select option to set Meadow Port on VS

### Deployment
You can use deployment option for running code in Meadow through VS 
