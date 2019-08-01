## JSON-RPC methods

* [getAccount](#getAccount)
* [lookUpName](#lookUpName)
* [getBlockHeight](#getBlockHeight)
* [getBlockTransactionCountByHash](#getBlockTransactionCountByHash)
* [getBlockByHash](#getBlockByHash)
* [getRawBlockByHash](#getRawBlockByHash)
* [getBlockByHeight](#getBlockByHeight)
* [getRawBlockByHeight](#getRawBlockByHeight)
* [getTransactionByBlockHashAndIndex](#getTransactionByBlockHashAndIndex)
* [getAddressTransactions](#getAddressTransactions)
* [getAddressTransactionCount](#getAddressTransactionCount)
* [sendRawTransaction](#sendRawTransaction)
* [invokeRawScript](#invokeRawScript)
* [getTransaction](#getTransaction)
* [cancelTransaction](#cancelTransaction)
* [getChains](#getChains)
* [getTokens](#getTokens)
* [getToken](#getToken)
* [getTokenData](#getTokenData)
* [getApps](#getApps)
* [getTokenTransfers](#getTokenTransfers)
* [getTokenTransferCount](#getTokenTransferCount)
* [getTokenBalance](#getTokenBalance)
* [getAuctionsCount](#getAuctionsCount)
* [getAuctions](#getAuctions)
* [getAuction](#getAuction)
* [getArchive](#getArchive)

## JSON RPC API Reference

***


#### getAccount
Returns the account name and balance of given address.


##### Parameters

1. `string` Address of account.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`Account` - A Account object, or `error` if address is invalid.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getAccount","params":[""],"id":1}'

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


#### lookUpName
Returns the address that owns a given name.


##### Parameters

1. `string` Name of account.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`string` - A string object, or `error` if address is invalid.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"lookUpName","params":[""],"id":1}'

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


#### getBlockHeight
Returns the height of a chain.


##### Parameters

1. `string` Address or name of chain.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`number` - A number, or `error` if chain is invalid.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getBlockHeight","params":[""],"id":1}'

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


#### getBlockTransactionCountByHash
Returns the number of transactions of given block hash or error if given hash is invalid or is not found.


##### Parameters

1. `string` Hash of block.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`number` - A number, or `error` if block hash is invalid.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getBlockTransactionCountByHash","params":[""],"id":1}'

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


#### getBlockByHash
Returns information about a block by hash.


##### Parameters

1. `string` Hash of block.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`Block` - A Block object, or `error` if block hash is invalid.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getBlockByHash","params":[""],"id":1}'

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


#### getRawBlockByHash
Returns a serialized string, containing information about a block by hash.


##### Parameters

1. `string` Hash of block.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`string` - A string object, or `error` if block hash is invalid.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getRawBlockByHash","params":[""],"id":1}'

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


#### getBlockByHeight
Returns information about a block by height and chain.


##### Parameters

1. `string` Address or name of chain.2. `number` Height of block.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`Block` - A Block object, or `error` if block hash is invalidor chain is invalid.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getBlockByHeight","params":["", ""],"id":1}'

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


#### getRawBlockByHeight
Returns a serialized string, in hex format, containing information about a block by height and chain.


##### Parameters

1. `string` Address or name of chain.2. `number` Height of block.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`string` - A string object, or `error` if block hash is invalidor chain is invalid.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getRawBlockByHeight","params":["", ""],"id":1}'

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


#### getTransactionByBlockHashAndIndex
Returns the information about a transaction requested by a block hash and transaction index.


##### Parameters

1. `string` Hash of block.2. `number` Index of transaction.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`Transaction` - A Transaction object, or `error` if block hash is invalidor index transaction is invalid.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getTransactionByBlockHashAndIndex","params":["", ""],"id":1}'

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

1. `string` Address of account.2. `number` Index of page to return.3. `number` Number of items to return per page.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`AccountTransactions` - A AccountTransactions object, or `error` if address is invalidor page is invalidor pageSize is invalid.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getAddressTransactions","params":["", "", ""],"id":1}'

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


#### getAddressTransactionCount
Get number of transactions in a specific address and chain


##### Parameters

1. `string` Address of account.2. `string` Name or address of chain, optional.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`number` - A number, or `error` if address is invalidor chain is invalid.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getAddressTransactionCount","params":["", ""],"id":1}'

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


#### sendRawTransaction
Allows to broadcast a signed operation on the network, but it&apos;s required to build it manually.


##### Parameters

1. `string` Serialized transaction bytes, in hexadecimal format.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`string` - A string object, or `error` if rejected by mempoolor script is invalidor failed to decoded transaction.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"sendRawTransaction","params":[""],"id":1}'

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


#### invokeRawScript
Allows to invoke script based on network state, without state changes.


##### Parameters

1. `string` Address or name of chain.2. `string` Serialized script bytes, in hexadecimal format.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`Script` - A Script object, or `error` if script is invalidor failed to decoded script.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"invokeRawScript","params":["", ""],"id":1}'

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


#### getTransaction
Returns information about a transaction by hash.


##### Parameters

1. `string` Hash of transaction.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`Transaction` - A Transaction object, or `error` if hash is invalid.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getTransaction","params":[""],"id":1}'

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


#### cancelTransaction
Removes a pending transaction from the mempool.


##### Parameters

1. `string` Hash of transaction.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`string` - A string object, or `error` if hash is invalid.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"cancelTransaction","params":[""],"id":1}'

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


#### getChains
Returns an array of all chains deployed in Phantasma.


##### Parameters


none


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`Chain` - A Chain object.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getChains","params":[],"id":1}'

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


#### getTokens
Returns an array of tokens deployed in Phantasma.


##### Parameters


none


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`Token` - A Token object.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getTokens","params":[],"id":1}'

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


#### getToken
Returns info about a specific token deployed in Phantasma.


##### Parameters

1. `string` Token symbol to obtain info.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`Token` - A Token object.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getToken","params":[""],"id":1}'

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


#### getTokenData
Returns data of a non-fungible token, in hexadecimal format.


##### Parameters

1. `string` Symbol of token.2. `string` ID of token.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`TokenData` - A TokenData object.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getTokenData","params":["", ""],"id":1}'

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


#### getApps
Returns an array of apps deployed in Phantasma.


##### Parameters


none


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`App` - A App object.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getApps","params":[],"id":1}'

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


#### getTokenTransfers
Returns last X transactions of given token.


##### Parameters

1. `string` Token symbol.2. `number` Index of page to return.3. `number` Number of items to return per page.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`Transaction` - A Transaction object, or `error` if token symbol is invalidor page is invalidor pageSize is invalid.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getTokenTransfers","params":["", "", ""],"id":1}'

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


#### getTokenTransferCount
Returns the number of transaction of a given token.


##### Parameters

1. `string` Token symbol.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`number` - A number, or `error` if token symbol is invalid.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getTokenTransferCount","params":[""],"id":1}'

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


#### getTokenBalance
Returns the balance for a specific token and chain, given an address.


##### Parameters

1. `string` Address of account.2. `string` Token symbol.3. `string` Address or name of chain.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`Balance` - A Balance object, or `error` if address is invalidor token is invalidor chain is invalid.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getTokenBalance","params":["", "", ""],"id":1}'

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


#### getAuctionsCount
Returns the number of active auctions.


##### Parameters

1. `string` Chain address or name where the market is located.2. `string` Token symbol used as filter.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`number` - A number.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getAuctionsCount","params":["", ""],"id":1}'

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


#### getAuctions
Returns the auctions available in the market.


##### Parameters

1. `string` Chain address or name where the market is located.2. `string` Token symbol used as filter.3. `number` Index of page to return.4. `number` Number of items to return per page.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`Auction` - A Auction object.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getAuctions","params":["", "", "", ""],"id":1}'

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


#### getAuction
Returns the auction for a specific token.


##### Parameters

1. `string` Chain address or name where the market is located.2. `string` Token symbol.3. `string` Token ID.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`Auction` - A Auction object.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getAuction","params":["", "", ""],"id":1}'

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


#### getArchive
Returns info about a specific archive.


##### Parameters

1. `string` Archive hash.


```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`Archive` - A Archive object.

  - `address `: `string` - Given address.
  - `name`: `string` - Name of given address.
  - `balances`: `Array` - Array of balance objects.
  - `balance - chain`: `string` - Name of the chain.
  - `balance - symbol`: `string` - Token symbol.
  - `balance - amount`: `string` - Amount of tokens.
 
  
##### Example
```js
// Request
curl -X POST --data '{"jsonrpc":"2.0","method":"getArchive","params":[""],"id":1}'

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

