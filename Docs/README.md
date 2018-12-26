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
