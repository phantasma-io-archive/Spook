<p align="center">
  <img
    src="/logo.png"
    width="125px"
  >
</p>

<h1 align="center">Phantasma Spook</h1>

<p align="center">
  Swiss army knife for Phantasma developers
</p>

## Contents

- [Description](#description)
- [Node](#node)
- [Node OS Support](#node-os-support)
- [API](#api)
- [Compiler](#compiler)
- [Contributing](#contributing)
- [License](#license)

---

## Description

Spook implements various Phantasma standard features in a single easy to use tool. These tools can be leveraged from the SDK allowing developers to setup a full Integrated Development Environment(IDE) locally. 

To learn more about Phantasma, please read the [White Paper](https://phantasma.io/phantasma_whitepaper.pdf) and check the official repository.


## Node
For development of Phantasma applications it is recommended you run a Phantasma node locally. It acts as your own personal blockchain network. The following instructions explain how to do this on Windows. Other operating systems are also supported and instructions can be found in the following section of this document. You can later move to the official test network where multiple nodes are running, in order to test your dapp under a more realistic enviroment.

To get started download a pre-compiled build which comes bundled in the official SDK release: https://github.com/phantasma-io/PhantasmaSDK/releases/latest - the files you need reside under Tools\Spook in the .zip file

To bootstrap your own test net just run a single instance of Phantasma node using the following arguments:

Note - you will need .NET runtime 2.2 or higher installed on your desktop https://dotnet.microsoft.com/download

```
Spook.dll -node.wif=L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25 -nexus.name=simnet
#To enable the RPC server add this argument to the line above
-rpc.enabled=true
```

Otherwise you can compile the latest yourself from the source available in this repository.

To compile and publish the source code for you will need the following:

- A Windows PC that suports Visual Studio Community
  - Can be obtained here: https://visualstudio.microsoft.com/downloads/
- An installation of Visual Studio Community with the following extension
  - .NET Desktop Development
  
Pull or download the following GitHub Repositories
- PhantasmaChain [repository](https://github.com/phantasma-io/PhantasmaChain) 
- PhantasmaSpook [repository](https://github.com/phantasma-io/PhantasmaSpook)

Ensure both of these sit in the same root directory on your PC and are in folders that match the above. For example:
- C:\<my code>\Phantasma\PhantasmaChain
- C:\<my code>\Phantasma\PhantasmaSpook

Build and publish the code
- Open Visual Studio
- Open the PhantasmaSpook\Spook.sln solution
- Build the solution
- Publish the Spook.CLI Project

The files needed to run a node will now be in PhantasmaSpook\Phantasma.CLI\Publish

As per above you can run it with the following command:

```
Spook.dll -node.wif=L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25 -nexus.name=simnet
#To enable the RPC server add this argument to the line above
-rpc.enabled=true
```

## Node OS Support

This section provides instructions on standing up a self-contained node for Phantasma on various operating systems. The dependencies installed as part of the below steps however will remain consistent and as such can serve as a basis for anyone who will later switch to running a Test or Main Network Node.

Below are instructions needed for the various operating systems supported.

### Runtime Paramaters

````
-rpc.enabled=
#Options: 'true' or 'false'
#Enables or disables the RPC server

-node.wif=
#Key for the wallet - local test wallet is 'L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25'

-nexus.name=
#Name of the network - 'simnet' is the self contained network

-gui.enabled=
#Options: 'true' or 'false'
#Enables or disables the graphic when running spook - enabled doesn't work well in a scripted start

-simulator.enabled=
#Options: 'true' or 'false'
#Utilised when supporting Nachomen - Luchedores get ready for action in the startup when enabled
```` 

### Ubuntu 18.04+

- DO NOT DO THIS AS ROOT!
- To find out your version of Ubuntu run this command in a terminal
````
lsb_release -a
````
- If it’s 18.04+ use these steps, if it is 19.04+ follow the steps in the 19.04+ section
- Copy the compiled Spook files from the previous section to somewhere on the filesystem
- Open a terminal and do the following to install .NET runtime

````
#Create the MS license
wget -q https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb 
sudo dpkg -i packages-microsoft-prod.deb
#Install the runtime
sudo add-apt-repository universe
sudo apt-get install apt-transport-https
sudo apt-get update
sudo apt-get install dotnet-runtime-2.2
````
-	If you get error like: Unable to locate package dotnet-runtime-2.2 – try the following

````
sudo dpkg --purge packages-microsoft-prod && sudo dpkg -i packages-microsoft-prod.deb 
sudo apt-get update 
sudo apt-get install dotnet-runtime-2.2
````

- In a terminal navigate to the location you placed the compiled contents of Spook
- Then run the instance 

````
dotnet Spook.dll -node.wif=L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25 -nexus.name=simnet -rpc.enabled=true
#to disable the graphic in the terminal, add the following argument to the line above
-gui.enabled=false
````
- An example shell script to execute the above and run it in the background as a process is
````
#!/bin/bash
dotnet Spook.dll -node.wif=L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25 -nexus.name=simnet -rpc.enabled=true -gui.enabled=false &
````

### Ubuntu 19.04+

- DO NOT DO THIS AS ROOT!
- To find out your version of Ubuntu run this command in a terminal
````
lsb_release -a
````

- If it’s 19.04+ use these steps, if it is 18.04+ follow the steps in the 18.04+ section
- Copy the compiled Spook files from the previous section to somewhere on the filesystem
- Open a terminal and do the following to install .NET runtime

````
#create the MS license
wget -q https://packages.microsoft.com/config/ubuntu/19.04/ -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
#Install the runtime
sudo add-apt-repository universe
sudo apt-get install apt-transport-https
sudo apt-get update
sudo apt-get install dotnet-runtime-2.2
````

- If you get error like: Unable to locate package dotnet-runtime-2.2 – try the following

````
sudo dpkg --purge packages-microsoft-prod && sudo dpkg -i packages-microsoft-prod.deb 
sudo apt-get update 
sudo apt-get install dotnet-runtime-2.2
````
- In a terminal navigate to the location you placed the compiled contents of Spook
- Then run the instance
````
dotnet Spook.dll -node.wif=L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25 -nexus.name=simnet -rpc.enabled=true
#to disable the graphic in the terminal, add the following argument to the line above
-gui.enabled=false  
````
### macOS 
- Copy the compiled Spook files from the previous section to somewhere on the filesystem
- Install .NET runtime
https://dotnet.microsoft.com/download/thank-you/dotnet-runtime-2.2.5-macos-x64-installer
- Open a terminal window
- In a terminal navigate to the location you placed the compiled contents of Spook
- Then run the instance
````
dotnet Spook.dll -node.wif=L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25 -nexus.name=simnet -rpc.enabled=true
#to disable the graphic in the terminal, add the following argument to the line above
-gui.enabled=false 
````

### CentOS
- DO NOT DO THIS AS ROOT!
- Copy the compiled Spook files from the previous section to somewhere on the filesystem
- Open a terminal and do the following to install .NET runtime

````
sudo rpm -Uvh https://packages.microsoft.com/config/rhel/7/packages-microsoft-prod.rpm
sudo yum update
sudo yum install dotnet-runtime-2.2
````
- In a terminal navigate to the location you placed the compiled contents of Spook
- Then run the instance
````
dotnet Spook.dll -node.wif=L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25 -nexus.name=simnet -rpc.enabled=true
#to disable the graphic in the terminal, add the following argument to the line above
-gui.enabled=false 
````

- An example shell script to execute the above and run it in the background as a process is

````
#!/bin/bash
dotnet Spook.dll -node.wif=L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25 -nexus.name=simnet -rpc.enabled=true -gui.enabled=false &
````
### Running in a screen - Linux

- For running for extended periods it makes sense to use a persistant screen so you can check the output whenever you like:
- Check if you have screen installed
````
screen --version
````
- If you don't do the following in debian
````	
 sudo apt install screen
````
CentOS/Fedora
````	
 sudo yum install screen
````
To run in a screen
````
screen -S SpeckyRules
./go specky.sh
````
- My shell script from above FYI looks like this
````
#!/bin/bash
dotnet ./Phantasma/Spook.dll -node.wif=L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25 -nexus.name=simnet -rpc.enabled=true -gui.enabled=false &
````

To list screens
````
screen -ls
````
To reconnect to the screen
````
screen -d -r <number of screen from previous command>
````
## API

Spook can optionally expose a RPC-JSON API so that you can connect it to your Phantasma dapps.

Documentation for this API can be found here: https://github.com/phantasma-io/PhantasmaSpook/tree/Nacho/Docs.

To enable the API pass the following flag to the Spook node you stood up in previous sections - examples of full commands are in the sections above:

```
-rpc.enabled=true
```

## Compiler

Spook comes with a builtin Phantasma smart contract assembler and compiler. 

More languages will be available later.

Language 		| Core Library	| Smart Compiler | Sample Dapps
:---------------------- | :------------| :------------| :------------
[.NET / C#](/C#) 		| Beta | In Development | Yes
[PHP](/PHP) 		| Beta | N/A | Yes |
[Python](/Python) 		| Beta | Planned | In Development |
[Golang](/Go) 		| Beta | Planned | In Development |
[Javascript](/JS) 		| Alpha | Planned | In Development |
[C++](/C++) 		| Alpha | Planned | In Development |
[Java](/Java) 		| Alpha | Planned | In Development |


## Contributing

You can contribute to Phantasma with [issues](https://github.com/PhantasmaProtocol/PhantasmaChain/issues) and [PRs](https://github.com/PhantasmaProtocol/PhantasmaChain/pulls). Simply filing issues for problems you encounter is a great way to contribute. Contributing implementations is greatly appreciated.

## License

The Phantasma project is released under the MIT license, see `LICENSE.md` for more details.
