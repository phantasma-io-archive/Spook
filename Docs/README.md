#### getAccount
Returns the account name and balance of given address.


##### Parameters

1. `String`, 45 length string - address to check for balance and name.
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

1. `String`, 45 length string - address to check for balance and name.
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
  - `txs`: `Array` - Array of transaction objects.
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

1. `String`, 45 length string - address to query transaction count.
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
Returns information about a block by hash or `error` if given hash is invalid or is not found.


##### Parameters


1. `DATA`, 34 bytes - hash of given block
```js
params: [
   '0x4C8D0DA35EF24DAE6F5BBAC8A11597A0EAB25926A3A474A28AD87C7F7792F6F2'
]
```

##### Returns

Object - A block object:

  - `hash`: `DATA`, 34 bytes - Block hash.
  - `previousHash`: `DATA` - Hash of previous block.
  - `timestamp`: `QUANTITY` - Block timestamp.
  - `height`: `QUANTITY` - Block height.
  - `chainAddress`: `string` - Chain address.
  - `nonce`: `DATA` - Nonce.
  - `reward`: `QUANTITY` - Reward given to the block miner.
  - `payload`: `DATA` - Custom data given by miners.
  - `txs`: `Array` - List of transactions inside this block.
 

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
Returns information about a block by height and chain or `error` if given height or chain are invalid or not found.


##### Parameters


1. `string`, - chain name or chain address
2. `QUANTITY`, - height of given block
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

`QUANTITY` - integer of the current block number the client is on.

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
