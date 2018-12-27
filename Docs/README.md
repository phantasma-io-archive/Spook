## JSON-RPC methods

* [getAccount](#getAccount)
* [getAddressTransactions](#getAddressTransactions)
* [getAddressTxCount](#getAddressTxCount)
* [getApps](#getApps)
* [getBlockByHash](#getBlockByHash)
* [getBlockByHeight](#getBlockByHeight)
* [getBlockHeight](#getBlockHeight)
* [getBlockTransactionCountByHash](#getBlockTransactionCountByHash)
* [getChains](#getChains)
* [getConfirmations](#getConfirmations)
* [getTransactionByHash](#getTransactionByHash)
* [getTransactionByBlockHashAndIndex](#getTransactionByBlockHashAndIndex)
* [getTokens](#getTokens)
* [getTokenTransfers](#getTokenTransfers)
* [getTokenTransferCount](#getTokenTransferCount)
* [sendRawTransaction](#sendRawTransaction)


## JSON RPC API Reference

***

#### getAccount
Returns the account name and balance of given address.


##### Parameters

1. `String`, base58 encoded - address to check for balance and name.
```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`Object` - An account object, or `error` if address is invalid or on a incorrect format

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getAccount","params":["PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV"],"id":1}'

// Result
{
   "jsonrpc":"2.0",
   "result":{
      "address":"P16m9XNDHxUex9hsGRytzhSj58k6W7BT5Xsvs3tHjJUkX",
      "name":"genesis",
      "balances":[
         {
            "chain":"main",
            "amount":"511567851650398",
            "symbol":"SOUL"
         },
         {
            "chain":"apps",
            "amount":"891917855944784",
            "symbol":"SOUL"
         }
      ]
   },
   "id":"1"
}
```

***

#### getAddressTransactions
Returns last X transactions of given address.


##### Parameters

1. `String`, base58 encoded - address to check for balance and name.
2. `QUANTITY`, number of last transactions.
```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV',
   5
]
```

##### Returns

`Object` - An array of transaction objects, or `error` if address is invalid or on a incorrect format

  - `address `: `string` - Given address.
  - `amount`: `QUANTITY` - Amount of transactions query.
  - `txs`: `Array` - Array of transaction objects. See [getTransactionByHash](#getTransactionByHash).
  - `txs - txid`: `DATA` - Transaction hash.
  - `txs - chainAddress`: `string` - Chain address.
  - `txs - chainName`: `string` - Chain name.
  - `txs - timestamp`: `long` - Timestamp of the transaction.
  - `txs - blockHeight`: `long` - Block height of chain in which the transaction occurred.
  - `txs - script`: `DATA` - Transaction script.
  - `txs - events`: `Array` - Array of the events occurred in the transaction.
  - `events - address`: `string` - Address on which the specific event occurred.
  - `events - data`: `DATA` - Serialized data of the event.
  - `events - kind`: `string` - Enum that specify the type of event. E.g: TokenSend.


##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getAddressTransactions","params":["PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV",3],"id":1}'

// Result
{
   "jsonrpc":"2.0",
   "result":{
      "address":"P16m9XNDHxUex9hsGRytzhSj58k6W7BT5Xsvs3tHjJUkX",
      "amount":3,
      "txs":[
         {
            "txid":"0xF1BA00567920AC884E1C0244ADDC21FF5E4541D7B1B9651FEE10442374214822",
            "chainAddress":"NztsEZP7dtrzRBagogUYVp6mgEFbhjZfvHMVkd2bYWJfE",
            "chainName":"main",
            "timestamp":1536498900,
            "blockHeight":462,
            "script":"030004036761732B0001030003020F2704000300030101040003000220913E7B38E0FC5239792071414821AACA71737EFE15F5E88988A0BA8EB90EABA4040003000409416464726573732829080003000408416C6C6F7747617304002C0103000405746F6B656E2B0001030003055A55B5D110040003000404534F554C040003000220107A56D57F87DD59B4C82EAC953EB255220F6260F5D7418BF9BCB6A1372327B0040003000409416464726573732829080003000220913E7B38E0FC5239792071414821AACA71737EFE15F5E88988A0BA8EB90EABA404000300040941646472657373282908000300040E5472616E73666572546F6B656E7304002C01030004036761732B000103000220913E7B38E0FC5239792071414821AACA71737EFE15F5E88988A0BA8EB90EABA40400030004094164647265737328290800030004085370656E6447617304002C010C",
            "events":[
               {
                  "address":"P9mQoUpwt5yxe8Jnrf4N16foQLW9RY67KbD7B2mtnM1BH",
                  "data":"0101020F27",
                  "kind":"GasEscrow"
               },
               {
                  "address":"P9mQoUpwt5yxe8Jnrf4N16foQLW9RY67KbD7B2mtnM1BH",
                  "data":"04534F554C055A55B5D1100D6E4079E36703EBD37C00722F5891D28B0E2811DC114B129215123ADCCE3605",
                  "kind":"TokenSend"
               },
               {
                  "address":"P16m9XNDHxUex9hsGRytzhSj58k6W7BT5Xsvs3tHjJUkX",
                  "data":"04534F554C055A55B5D1100D6E4079E36703EBD37C00722F5891D28B0E2811DC114B129215123ADCCE3605",
                  "kind":"TokenReceive"
               },
               {
                  "address":"P9mQoUpwt5yxe8Jnrf4N16foQLW9RY67KbD7B2mtnM1BH",
                  "data":"01010168",
                  "kind":"GasPayment"
               }
            ]
         },{...}         
            ]
         }
      ]
   },
   "id":"1"
}
```

***

#### getAddressTxCount
Returns the number of transaction of given address.


##### Parameters

1. `String`, base58 encoded - address to query transaction count.
```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`QUANTITY` - Integer of the number of transactions send from this address.
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getAddressTxCount","params":["PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV"],"id":1}'

// Result
{
   "jsonrpc":"2.0",
   "result":"42",
   "id":"1"
}
```

***

#### getApps
Returns an array of application running on Phantasma.


##### Parameters

none

##### Returns

`apps`: `Array` - Set of applications descriptions.

  - `description `: `string` - Brief application description.
  - `icon `: `DATA` - Small application icon.
  - `id `: `string` - Application ID.
  - `title `: `string` - Application title.
  - `url `: `string` - Url of the application website.

##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getApps","params":[],"id":1}'

// Result
{
   "jsonrpc":"2.0",
   "result":{
      "apps":[
         {
            "description":"Collect, train and battle against other players in Nacho Men!",
            "icon":"0x0000000000000000000000000000000000000000000000000000000000000000",
            "id":"nachomen",
            "title":"nachomen",
            "url":"https:\/\/nacho.men"
         },
         {
            "description":"The future of digital content distribution!",
            "icon":"0x0000000000000000000000000000000000000000000000000000000000000000",
            "id":"mystore",
            "title":"mystore",
            "url":"https:\/\/my.store"
         }
      ]
   },
   "id":"1"
}
```

***

#### getBlockByHash
Returns information about a block by hash.

##### Parameters


1. `DATA`, 33 bytes - hash of a block
```js
params: [
   '0x4C8D0DA35EF24DAE6F5BBAC8A11597A0EAB25926A3A474A28AD87C7F7792F6F2'
]
```

##### Returns

Object - A block object or `error` if given hash is invalid or is not found:

  - `hash`: `DATA`, 33 bytes - Block hash.
  - `previousHash`: `DATA` - Hash of previous block.
  - `timestamp`: `QUANTITY` - Block timestamp.
  - `height`: `QUANTITY` - Block height.
  - `chainAddress`: `string` - Chain address.
  - `nonce`: `DATA` - Nonce.
  - `reward`: `QUANTITY` - Reward given to the block miner.
  - `payload`: `DATA` - Custom data given by miners.
  - `txs`: `Array` - List of transactions inside this block. See [getTransactionByHash](#getTransactionByHash).
 

##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getBlockByHash","params":["0x4C8D0DA35EF24DAE6F5BBAC8A11597A0EAB25926A3A474A28AD87C7F7792F6F2"],"id":1}'

// Result
{
   "jsonrpc":"2.0",
   "result":{
      "hash":"0x4C8D0DA35EF24DAE6F5BBAC8A11597A0EAB25926A3A474A28AD87C7F7792F6F2",
      "previousHash":"0xCC456422FF4599FB0EB8A78C0FA783A66E08A9565E638904F7FEB67367CF06A9",
      "timestamp":"26\/12\/2018 15:56:36",
      "height":563,
      "chainAddress":"NztsEZP7dtrzRBagogUYVp6mgEFbhjZfvHMVkd2bYWJfE",
      "nonce":0,
      "minerAddress":"P16m9XNDHxUex9hsGRytzhSj58k6W7BT5Xsvs3tHjJUkX",
      "reward":0.0000052,
      "payload":"",
      "txs":[
         {
            "txid":"0x92A476D9E3FC4CFD810E5DA3840DF32A3DA3BAC9C58E3F2C868491D357CA527A",
            "chainAddress":"NztsEZP7dtrzRBagogUYVp6mgEFbhjZfvHMVkd2bYWJfE",
            "chainName":"main",
            "timestamp":1545839796,
            "blockHeight":563,
            "script":"030004036761732B0001030003020F2704000300030101040003000220E9B876646F83E08D477606947C1CF8305B44F69408E9D805EED7E466B84C6EEB040003000409416464726573732829080003000408416C6C6F7747617304002C0103000405746F6B656E2B0001030003058B390CBAF7040003000404534F554C0400030002208ED1352401D148977B19D1228F6C9847292F0C164C5C2478E9A09F4CF6DC416B040003000409416464726573732829080003000220E9B876646F83E08D477606947C1CF8305B44F69408E9D805EED7E466B84C6EEB04000300040941646472657373282908000300040E5472616E73666572546F6B656E7304002C01030004036761732B000103000220E9B876646F83E08D477606947C1CF8305B44F69408E9D805EED7E466B84C6EEB0400030004094164647265737328290800030004085370656E6447617304002C010C",
            "events":[
               {
                  "address":"PFinZTCKezYYXMqVaVGNndhkQcuqPW7AmnLrU9jCRQeKk",
                  "data":"0101020F27",
                  "kind":"GasEscrow"
               },
               {
                  "address":"PFinZTCKezYYXMqVaVGNndhkQcuqPW7AmnLrU9jCRQeKk",
                  "data":"04534F554C058B390CBAF70D6E4079E36703EBD37C00722F5891D28B0E2811DC114B129215123ADCCE3605",
                  "kind":"TokenSend"
               },
               {
                  "address":"P9bwLwG8hoq52cgizgJ5kt5XaAkrzfTn6ZiUkQTPbUfEA",
                  "data":"04534F554C058B390CBAF70D6E4079E36703EBD37C00722F5891D28B0E2811DC114B129215123ADCCE3605",
                  "kind":"TokenReceive"
               },
               {
                  "address":"PFinZTCKezYYXMqVaVGNndhkQcuqPW7AmnLrU9jCRQeKk",
                  "data":"01010168",
                  "kind":"GasPayment"
               }
            ]
         },{...}
      ]
   },
   "id":"1"
}
```

***

#### getBlockByHeight
Returns information about a block by height and chain.


##### Parameters


1. `string`, - chain name or chain address
2. `QUANTITY`, - height of a block
```js
params: [
   'main',
   1
]
```

##### Returns

See [getBlockByHash](#getblockbyhash)

##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getBlockByHeight","params":["main",1],"id":1}'
```

Result see [getBlockByHash](#getblockbyhash)


***


#### getBlockHeight
Returns the height of most recent block of given chain.

##### Parameters

1. `string`, - chain name or chain address

##### Returns

`QUANTITY` - Integer of the current block number the client is on.

##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getBlockHeight","params":["main"],"id":1}'

// Result
{
   "jsonrpc":"2.0",
   "result":"540",
   "id":"1"
}
```


***


#### getBlockTransactionCountByHash
Returns the number of transactions of given block hash or `error` if given hash is invalid or is not found.

##### Parameters

1. `DATA`, 33 bytes - hash of a block
```js
params: [
   '0x4C8D0DA35EF24DAE6F5BBAC8A11597A0EAB25926A3A474A28AD87C7F7792F6F2'
]
```

##### Returns

`QUANTITY` - integer of the number of transactions in this block.

##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getBlockTransactionCountByHash","params":["0x4C8D0DA35EF24DAE6F5BBAC8A11597A0EAB25926A3A474A28AD87C7F7792F6F2"],"id":1}'

// Result
{
   "jsonrpc":"2.0",
   "result":"540",
   "id":"1"
}
```

***

#### getChains
Returns an array of chains with useful information.

##### Parameters

none

##### Returns

Array of chain info:

  - `name`: `string` - Chain name.
  - `address`: `string` - Chain address.
  - `height`: `QUANTITY` - Last block number.
  - `children`: `Array` - Child chains.
 

##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getChains","params":[],"id":1}'

// Result
{
   "jsonrpc":"2.0",
   "result":[
      {
         "name":"main",
         "address":"NztsEZP7dtrzRBagogUYVp6mgEFbhjZfvHMVkd2bYWJfE",
         "height":504,
         "children":[
            {
               "name":"privacy",
               "address":"PB5k5d7rbdNU5QKHMgErzw8Mkqx9TZh8FcfE8oTfdqn2v"
            },
            {
               "name":"vault",
               "address":"PFXw1o59Kshau2rPXKRhVVLmKwfMh2eyNzBbdqNoSDkwx"
            },
            {
               "name":"bank",
               "address":"P4XxbH98DUM59KCQogauUxWxsyuQJ1wbfoCDQUhryhXK1"
            },
            {
               "name":"apps",
               "address":"PEMbn8smAMZxbGVFCcLMWbQZBYK7SFf93jFLFi8dZV6Ga"
            }
         ]
      },
      {
         "name":"privacy",
         "address":"PB5k5d7rbdNU5QKHMgErzw8Mkqx9TZh8FcfE8oTfdqn2v",
         "height":1,
         "parentAddress":"main"
      },
      {
         "name":"vault",
         "address":"PFXw1o59Kshau2rPXKRhVVLmKwfMh2eyNzBbdqNoSDkwx",
         "height":1,
         "parentAddress":"main"
      },{...}
   ],
   "id":"1"
}
```

***

#### getConfirmations
Returns the number of confirmations of given transaction hash and other useful info.

##### Parameters

1. `DATA`, 33 bytes - hash of a transaction
```js
params: [
   '0x34647C9A097909C7E5112B7F8F3950F6FA65D20DFA9D172A8F5084AC8595EABD'
]
```
##### Returns

Array of chain info:

  - `confirmations`: `QUANTITY` - Chain name.
  - `hash`: `DATA` - Chain address.
  - `height`: `QUANTITY` - Last block number.
  - `chain`: `string` - Child chains.
 

##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getConfirmations","params":["0x34647C9A097909C7E5112B7F8F3950F6FA65D20DFA9D172A8F5084AC8595EABD"],"id":1}'

// Result
{
   "jsonrpc":"2.0",
   "result":{
      "confirmations":1,
      "hash":"0x34647C9A097909C7E5112B7F8F3950F6FA65D20DFA9D172A8F5084AC8595EABD",
      "height":510,
      "chain":"NztsEZP7dtrzRBagogUYVp6mgEFbhjZfvHMVkd2bYWJfE"
   },
   "id":"1"
}
```

***

#### getTransactionByHash
Returns the information about a transaction requested by transaction hash.

##### Parameters

1. `DATA`, 33 bytes - hash of a transaction
```js
params: [
   '0x3C0D260AACF17BD4AFA535C4845E1CE8B9D8A600A826AB138ADF677C6369C703'
]
```
##### Returns

Object - A transaction object or `error` if hash is invalid or not found:

  - `txs - txid`: `DATA` - Transaction hash.
  - `txs - chainAddress`: `string` - Chain address.
  - `txs - chainName`: `string` - Chain name.
  - `txs - timestamp`: `long` - Timestamp of the transaction.
  - `txs - blockHeight`: `long` - Block height of chain in which the transaction occurred.
  - `txs - script`: `DATA` - Transaction script.
  - `txs - events`: `Array` - Array of the events occurred in the transaction.
  - `events - address`: `string` - Address on which the specific event occurred.
  - `events - data`: `DATA` - Serialized data of the event.
  - `events - kind`: `string` - Enum that specify the type of event. E.g: TokenSend.
 

##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getTransactionByHash","params":["0x3C0D260AACF17BD4AFA535C4845E1CE8B9D8A600A826AB138ADF677C6369C703"],"id":1}'

// Result
{
   "jsonrpc":"2.0",
   "result":{
      "txid":"0x3C0D260AACF17BD4AFA535C4845E1CE8B9D8A600A826AB138ADF677C6369C703",
      "chainAddress":"NztsEZP7dtrzRBagogUYVp6mgEFbhjZfvHMVkd2bYWJfE",
      "chainName":"main",
      "timestamp":1545844818,
      "blockHeight":515,
      "script":"030004036761732B0001030003020F270400030003010104000300022042CDF84A890B8E2649D6C9A6D643B09D15A01BB64F5EE0816A8EF8F341055096040003000409416464726573732829080003000408416C6C6F7747617304002C0103000405746F6B656E2B0001030003061E4ED9E77901040003000404534F554C040003000220A81C7F808DE996F8C61E01F2488CA69F93A21E76949075BCCDAE03245C14B2E004000300040941646472657373282908000300022042CDF84A890B8E2649D6C9A6D643B09D15A01BB64F5EE0816A8EF8F34105509604000300040941646472657373282908000300040E5472616E73666572546F6B656E7304002C01030004036761732B00010300022042CDF84A890B8E2649D6C9A6D643B09D15A01BB64F5EE0816A8EF8F3410550960400030004094164647265737328290800030004085370656E6447617304002C010C",
      "events":[
         {
            "address":"P4VDVj2xoHXjShSaCxHWZDEuy8pBcTe56msuWJACQyhcy",
            "data":"0101020F27",
            "kind":"GasEscrow"
         },
         {
            "address":"P4VDVj2xoHXjShSaCxHWZDEuy8pBcTe56msuWJACQyhcy",
            "data":"04534F554C061E4ED9E779010D6E4079E36703EBD37C00722F5891D28B0E2811DC114B129215123ADCCE3605",
            "kind":"TokenSend"
         },
         {
            "address":"PBJg8AF9ta8nEHF3MidzzofTj4WDBRWAoHv2WQDWwcyGb",
            "data":"04534F554C061E4ED9E779010D6E4079E36703EBD37C00722F5891D28B0E2811DC114B129215123ADCCE3605",
            "kind":"TokenReceive"
         },
         {
            "address":"P4VDVj2xoHXjShSaCxHWZDEuy8pBcTe56msuWJACQyhcy",
            "data":"01010168",
            "kind":"GasPayment"
         }
      ]
   },
   "id":"1"
}
```

***


#### getTransactionByBlockHashAndIndex
Returns the information about a transaction requested by a block hash and transaction index.

##### Parameters

1. `DATA`, 33 bytes - hash of a block
```js
params: [
   '0x1EBA956A82900DCB69B3A372EBE558760913ACEF9292917C9E43DFB685DEDDA5',
   5
]
```
##### Returns

See [getTransactionByHash](#getTransactionByHash).
 

##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getTransactionByBlockHashAndIndex","params":["0x1EBA956A82900DCB69B3A372EBE558760913ACEF9292917C9E43DFB685DEDDA5", 5],"id":1}'
```
Result
See [getTransactionByHash](#getTransactionByHash).

***

#### getTokens
Returns an array of tokens deployed in Phantasma.


##### Parameters

none

##### Returns

Array of token info:

  - `symbol`: `string` - Unique token symbol.
  - `name`: `string` - Full token name.
  - `currentSupply`: `QUANTITY` - Token current supply.
  - `maxSupply`: `QUANTITY` - Token maximum supply.
  - `decimals`: `QUANTITY` - Token decimals.
  - `isFungible`: `bool` - Indicates if the token is fungible or not.
  - `flags`: `string` - Set of token properties.
  - `owner`: `string` - Address of token owner.   
 

##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getTokens","params":[],"id":1}'


//Result
{
   "jsonrpc":"2.0",
   "result":{
      "tokens":[
         {
            "symbol":"SOUL",
            "name":"Phantasma",
            "currentSupply":"8648333245505330",
            "maxSupply":"9113637400000000",
            "decimals":8,
            "isFungible":true,
            "flags":"Transferable, Fungible, Finite, Divisible",
            "owner":"P16m9XNDHxUex9hsGRytzhSj58k6W7BT5Xsvs3tHjJUkX"
         },
         {
            "symbol":"ALMA",
            "name":"Stable Coin",
            "currentSupply":"0",
            "maxSupply":"0",
            "decimals":8,
            "isFungible":true,
            "flags":"Transferable, Fungible, Divisible",
            "owner":"P16m9XNDHxUex9hsGRytzhSj58k6W7BT5Xsvs3tHjJUkX"
         },
         {
            "symbol":"NACHO",
            "name":"Nachomen",
            "currentSupply":"0",
            "maxSupply":"0",
            "decimals":0,
            "isFungible":false,
            "flags":"Transferable",
            "owner":"P16m9XNDHxUex9hsGRytzhSj58k6W7BT5Xsvs3tHjJUkX"
         }
      ]
   },
   "id":"1"
}
```

***


#### getTokenTransfers
Returns last X transactions of given token.

##### Parameters

1. `string` - token symbol
2. `QUANTITY` - amount of transactions to query
```js
params: [
   'SOUL',
   5
]
```

##### Returns

See [getTransactionByHash](#getTransactionByHash). 

##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getTokenTransfers","params":["SOUL", 5],"id":1}'
```

Result

See [getTransactionByHash](#getTransactionByHash). 

***

#### getTokenTransferCount
Returns the number of transaction of a given token.

##### Parameters

1. `string` - token symbol
```js
params: [
   'SOUL'
]
```

##### Returns

`QUANTITY` - Integer of the number of transactions of given token.

##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getTokenTransferCount","params":["SOUL"],"id":1}'


//Result
{
   "jsonrpc":"2.0",
   "result":"2245",
   "id":"1"
}

```

***

#### sendRawTransaction
Allows to broadcast a signed operation on the network, but it's required to build it manually. 

##### Parameters

1. `DATA` - The signed transaction data.
```js
params: [
   '0xd46e8dd67c5d32be8d46e8dd67c5d32be8058bb8eb970870f072445675058bb8eb970870f072445675'
]
```

##### Returns

`DATA` - The transaction hash, or null if the transaction does not make it to the mempool for some reason.

##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"sendRawTransaction","params":["0xd46e8dd67c5d32be8d46e8dd67c5d32be8058bb8eb970870f072445675058bb8eb970870f072445675"],"id":1}'


//Result
{
   "jsonrpc":"2.0",
   "result":{
      "hash":"0x3C0D260AACF17BD4AFA535C4845E1CE8B9D8A600A826AB138ADF677C6369C703"
      },
   "id":"1"
}

```

***
