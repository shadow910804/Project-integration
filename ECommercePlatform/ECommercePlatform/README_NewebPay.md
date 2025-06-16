# NewebPay 藍新金流串接說明（測試環境）

本專案已整合 NewebPay 測試金流，可使用以下參數進行付款流程模擬。

---

## ✅ 測試帳號資訊

| 項目         | 測試值                                  |
|--------------|-----------------------------------------|
| MerchantID   | MS3448209                               |
| HashKey      | 8NnPzWnFzvK0M4iiq1r8H9Vb8RvgL7yW         |
| HashIV       | lzB3qWZy8dkA4Dgz                         |
| RespondURL   | /Payment/Notify                         |
| ReturnURL    | /Order/Confirm?id={訂單編號}            |

---

## ✅ 串接流程說明

1. 使用者結帳完成後，導向 `/Order/Pay/{id}` 發送付款請求至 NewebPay。
2. 系統透過 `NewebPayHelper.cs` 對付款參數加密並產生 CheckValue。
3. 藍新付款頁完成後會：
   - 前景跳轉 `/Order/Confirm?id={id}`
   - 背景發送付款結果至 `/Payment/Notify`
4. 系統收到通知後驗證交易結果，並更新 `Order` 表中的付款狀態 `PaymentVerified = true`

---

## 📁 實作檔案列表

| 檔案路徑                              | 功能                     |
|---------------------------------------|--------------------------|
| Controllers/OrderController.cs        | 建立訂單 / 發送付款表單 |
| Controllers/PaymentController.cs      | 處理付款回傳通知         |
| Helpers/NewebPayHelper.cs             | 加密、簽章處理工具類別   |
| Views/Order/Pay.cshtml                | 自動提交表單至 NewebPay |

---

## ⚠️ 正式上線請替換

請將 `MerchantID`、`HashKey`、`HashIV` 及付款網址切換為藍新提供的正式帳號資訊。