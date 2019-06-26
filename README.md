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
- [API](#api)
- [Compiler](#compiler)
- [Contributing](#contributing)
- [License](#license)

---

## Description

Spook implements various Phantasma standard features in a single easy to use tool.

To learn more about Phantasma, please read the [White Paper](https://phantasma.io/phantasma_whitepaper.pdf) and check the official repository.

## Node

To development Phantasma applications it is recommended to run a Phantasma node locally.

Get either a pre-compiled build of Phantasma-CLI which comes bundled in the official SDK release or compile yourself from the source available in the official [repository](https://github.com/phantasma-io/PhantasmaNode)

To bootstrap your own test net just run a single instance of Phantasma node using the following arguments:
```
Spook.dll -node.wif=L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25 -nexus.name=simnet
```

Note - For a development purposes you can keep your testnet Phantasma network running with just one node. 

You can later move to the official test network where multiple nodes are running, in order to test your dapp under a more realistic enviroment.

## Wallet

Spook can do several operations related to wallet usage.

To view the balances of a wallet use the wallet.balance command. You can optionally not pass a specific address to view the balance of the local node.
```
-wallet.balance TARGET_ADDRESS

eg:
-wallet.balance PGbGitREtLZi89QGxSLtBfs51Ukufs5PzhC9kky8Tet93
```

To transfer funds from the local node to other address use the wallet.transfer command.
```
-wallet.transfer TARGET_ADDRESS AMOUNT SYMBOL
eg:
-wallet.transfer PGbGitREtLZi89QGxSLtBfs51Ukufs5PzhC9kky8Tet93 10.5 SOUL
```


## API

Spook can optionally expose a RPC-JSON API so that you can connect it to your Phantasma dapps.

Documentation for this API can be found [here](/Docs).

To enable the API pass the following flag to Spook:

```
-rpc.enabled=true
```

## Compiler

Spook comes with a builtin Phantasma smart contract assembler and compiler. 

More languages will be available later.

Language 		| Status
:---------------------- | 
C# 		| In Development (30%) 
Solidity 		| In Development (25%) 


## Contributing

You can contribute to Phantasma with [issues](https://github.com/PhantasmaProtocol/PhantasmaChain/issues) and [PRs](https://github.com/PhantasmaProtocol/PhantasmaChain/pulls). Simply filing issues for problems you encounter is a great way to contribute. Contributing implementations is greatly appreciated.

## License

The Phantasma project is released under the MIT license, see `LICENSE.md` for more details.