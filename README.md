# UnicontaRest

## Run
```
docker run -e AffiliateKey="InsertKeyHere" -p 5000:80 rmjac/unicontarest
```

## Examples
```
curl -u username:password http://localhost:5000/Companies/:companyId/Query/DebtorOrder
curl -u username:password http://localhost:5000/Companies/:companyId/Query/DebtorOrder?filter._OrderNumber=1
curl -u username:password http://localhost:5000/Companies/:companyId/Query/DebtorOrder?filter.Created=1/1-2018..1/1-2019
curl -u username:password "http://localhost:5000/Companies/12114/Query/DebtorOrderLine?filter._OrderNumber=1&filter.rowId=2"
curl -u username:password -X POST -H "Content-Type: application/json" http://localhost:5000/Companies/12114/Crud/DebtorOrderLine --data "{_OrderNumber:1}"
curl -v -u username:password -X PATCH -H "Content-Type: application/json-patch+json" "http://localhost:5000/Companies/12114/Crud/DebtorOrderLine?filter._OrderNumber=1&filter.rowId=3&limit=1" --data '[{"op":"replace", "path":"/_Text", "value": "Hello World"}]'
curl -v -u username:password -X DELETE "http://localhost:5000/Companies/12114/Crud/DebtorOrderLine?filter._OrderNumber=1&filter.rowId=3&limit=1"
```