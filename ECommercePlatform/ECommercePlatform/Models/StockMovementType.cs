namespace ECommercePlatform.Models
{
    // 庫存變動類型枚舉
    // 定義所有可能的庫存變動原因
    public enum StockMovementType
    {
        // 銷售（庫存減少）
        Sale = 1,

        // 退貨（庫存增加）
        Return = 2,

        // 入庫調整（庫存增加）
        // 例如：進貨、盤點發現多餘庫存
        Adjustment_In = 3,

        // 出庫調整（庫存減少）
        // 例如：盤點發現庫存不足、管理員調整
        Adjustment_Out = 4,

        // 損耗（庫存減少）
        // 例如：商品損壞、過期等
        Damage = 5,

        // 調撥（可能增加或減少）
        // 例如：不同倉庫間的庫存調撥
        Transfer = 6,

        // 其他原因
        Other = 99
    }
}