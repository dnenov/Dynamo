## 深入資訊
Fillet 會傳回一個有圓邊的新實體。邊輸入指定要圓角的邊，偏移輸入決定圓角的半徑。在以下範例中，我們使用預設輸入從立方體開始。為了取得立方體適當的邊，我們先分解立方體取得各個面作為曲面的清單，然後使用 Face.Edges 節點擷取立方體的邊。我們使用 GetItemAtIndex 擷取每一面的第一個邊。數字滑棒控制每個圓角的半徑。
___
## 範例檔案

![Fillet](./Autodesk.DesignScript.Geometry.PolyCurve.Fillet_img.jpg)
