# PhantasmaExplorer
Block explorer for Phantasma Chain




#### getAccount
Returns the account name and balance of given address.


##### Parameters

1. `DATA`, 45 length string - address to check for balance and name.
```js
params: [
   'PDHcAHq1fZXuwDrtJGghjemFnj2ZaFc7iu3qD4XjZG9eV'
]
```

##### Returns

`Object` - An account object, or `error` if address is invalid or on a incorrect format

  - `address `: `DATA` - given address.
  - `name`: `DATA` - name of given address.
  - `balances`: `Array` - array of balance objects.
  - `balance - chain`: `DATA` - name of the chain.
  - `balance - symbol`: `DATA` - symbol of the token.
  - `balance - amount`: `DATA` - amount of tokens.
 
  
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

