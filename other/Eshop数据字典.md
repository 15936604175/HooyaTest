## scb_xsjl
> 销售记录表   
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | number |  | √ | Decimal（18,0） | 9 | 0 |  |  |  主键  |
| 2 | name  |  |  | nvarchar（50） | 100 | 0 | √ |  |     |
| 3 | depart  |  |  | nvarchar（50） | 100 | 0 | √ |  |  部门编号 |
| 4 | hthm  |  |  | nvarchar（50） | 100 | 0 | √ |  |  合同号码  |
| 5 | OrderId  |  |  | nvarchar（50） | 100 | 0 | √ |  |  订单编号  |
| 6 | newdateis  |  |  | datetime | 8 | 0 | √ |  |  订单下载时间(创建时间)     北京时间:下载时间  |
| 7 | moddateis  |  |  | datetime | 8 | 0 | √ |  |  订单修改时间(修改时间)     北京时间:最后一次被修改的时间   |
| 8 | orderdate  |  |  | datetime | 8 | 0 | √ |  |  订单时间     打单的人员看的时间  |
| 9 | fkzh  |  |  | nvarchar（50） | 100 | 0 | √ |  |  付款账号  |
| 10 | khname  |  |  | nvarchar（200） | 400 | 0 | √ |  |  客户姓名  |
| 11 | Address  |  |  | nvarchar（100） | 200 | 0 | √ |  |  客户地址  |
| 12 | lxfs  |  |  | varchar（50） | 50 | 0 | √ |  |  联系方式(电话)  |
| 13 | email  |  |  | varchar（300） | 300 | 0 | √ |  |  邮箱  |
| 14 | lywl  |  |  | varchar（50） | 50 | 0 | √ |  |  联系物流     Fba:货在亚马逊  |
| 15 | yf  |  |  | float | 8 | 0 | √ |  |  运费  |
| 16 | zhuizh  |  |  | varchar（50） | 50 | 0 | √ |  |     |
| 17 | xspt  |  |  | varchar（50） | 50 | 0 | √ |  |  销售平台  |111-8717440-8395431
| 18 | country  |  |  | varchar（50） | 50 | 0 | √ |  |  国家代码  |
| 19 | dianpu  |  |  | varchar（50） | 50 | 0 | √ |  |  店铺  |
| 20 | chck  |  |  | varchar（50） | 50 | 0 | √ |  |  出货仓库  |
| 21 | fkfs  |  |  | varchar（50） | 50 | 0 | √ |  |  付款方式  |
| 22 | fhdate  |  |  | datetime | 8 | 0 | √ |  |     |
| 23 | zsl  |  |  | int | 4 | 0 | √ |  |  总数量  |
| 24 | xszje  |  |  | float | 8 | 0 | √ |  |  销售总资金  |
| 25 | yfzje  |  |  | float | 8 | 0 | √ |  |  Ebay价格  |
| 26 | rxszje  |  |  | float | 8 | 0 | √ |  |     |
| 27 | ryfzje  |  |  | float | 8 | 0 | √ |  |     |
| 28 | bz  |  |  | varchar（50） | 50 | 0 | √ |  |  备注  |
| 29 | state  |  |  | int | 4 | 0 | √ |  |  订单状态     6:取消订单   2：其他订单（Vendor）   3：其他订单     4：暂停订单     0：正常订单     其他：未付款订单  |
| 30 | statsa  |  |  | nvarchar（50） | 100 | 0 | √ |  |  州     如：华盛顿州  |
| 31 | county  |  |  | nvarchar（50） | 100 | 0 | √ |  |  国家  |
| 32 | city  |  |  | nvarchar（50） | 100 | 0 | √ |  |  城市  |
| 33 | phone  |  |  | varchar（50） | 50 | 0 | √ |  |  电话  |
| 34 | others  |  |  | varchar（50） | 50 | 0 | √ |  |     |
| 35 | zip  |  |  | varchar（50） | 50 | 0 | √ |  |  目的地邮编  |
| 36 | Address1  |  |  | nvarchar（100） | 200 | 0 | √ |  |     |
| 37 | Ebayzje  |  |  | float | 8 | 0 | √ |  |     |
| 38 | creattime  |  |  | datetime | 8 | 0 | √ |  |  创建时间  |
| 39 | recordnumber  |  |  | nvarchar（50） | 100 | 0 | √ |  |  记录号  |
| 40 | zek  |  |  | float | 8 | 0 | √ |  |  折扣跟退金金额  |
| 41 | thstate  |  |  | int | 4 | 0 | √ |  |  退货转态  |
| 42 | thsl  |  |  | int | 4 | 0 | √ |  |  退货数量  |
| 43 | thwpzt  |  |  | int | 4 | 0 | √ |  |     |
| 44 | Zsyf  |  |  | float | 8 | 0 | √ |  |     |
| 45 | sfzm  |  |  | tinyint | 8 | 0 | √ |  |  是否转卖给tpp  |
| 46 | isly  |  |  | tinyint | 8 | 0 | √ |  |     |
| 47 | lytime  |  |  | varchar（50） | 50 | 0 | √ |  |     |
| 48 | trackno  |  |  | nvarchar（500） | 1000 | 0 | √ |  |  跟踪号  |
| 49 | trackfee  |  |  | float | 8 | 0 | √ |  |     |
| 50 | logicswayoid  |  |  | int | 4 | 0 | √ |  |  物流方式Id  |
| 51 | warehouseid  |  |  | int | 4 | 0 | √ |  |  仓库id  |
| 52 | filterflag  |  |  | int | 4 | 0 | √ |  |  订单阶段     4：待处理订单(自动)     5：待处理订单(手动)     7：待发货订单     10：已打订单     11：全部订单  |
| 53 | servicetype  |  |  | nvarchar（500） | 1000 | 0 | √ |  |  物流  |
| 54 | signtype  |  |  | tinyint | 8 | 0 | √ |  |  标记类别     0：普通     1：次要     2：紧急  |
| 55 | Fromcountry  |  |  | nvarchar（50） | 100 | 0 | √ |  |     |
| 56 | ismark  |  |  | int | 4 | 0 | √ |  |  标记发货     1：已完成     3：已完成     2：已完成  |
| 57 | B2CSHIPMENT  |  |  | nvarchar（50） | 100 | 0 | √ |  |     |
| 58 | TEMP1  |  |  | nvarchar（50） | 100 | 0 | √ |  |  标记状态     1：已标记     3：已标记     0：未标记  |
| 59 | TEMP2  |  |  | nvarchar（50） | 100 | 0 | √ |  |     | (企业订单B2B)
| 60 | TEMP3  |  |  | nvarchar（50） | 100 | 0 | √ |  |  1：退款退货     2:退款     3:退货补发     4:重发     5:补发配件     6:卡车运送     7:退货单     8:补发单     9:买家自提     其他:付款订单  | 12 预售
| 61 | TEMP4  |  |  | nvarchar（50） | 100 | 0 | √ |  |  原始数量  |
| 62 | TEMP5  |  |  | nvarchar（50） | 100 | 0 | √ |  |  交易流水  |
| 63 | TEMP6  |  |  | nvarchar（50） | 100 | 0 | √ |  |  记录OrderSearch操作     1退款操作     2退货操作（hooya）     3退货操作（customer）     4补发操作     5退款退货（hooya）     6退款退货（customer）     7退款补发     8退货补发（hooya）     9退货补发（customer）     10退款退货补发（hooya）     11退款退货补发（customer） 12退货单     13补发单     14配件补发  |
| 64 | TEMP7  |  |  | nvarchar（50） | 100 | 0 | √ |  |  处理结果     1：完成     2：准备申请(自动处理状态，订单(如波兰投递站))     5：替换过货号
| 65 | TEMP8  |  |  | nvarchar（50） | 100 | 0 | √ |  |  打单日期  |
| 66 | TEMP9  |  |  | nvarchar（50） | 100 | 0 | √ |  |     |
| 67 | TEMP10  |  |  | nvarchar（50） | 100 | 0 | √ |  |  收货国家  |
| 68 | _MASK_FROM_V2  |  |  | timestamp | 8 | 0 |   |  |     |
| 69 | curr  |  |  | nvarchar（20） | 40 | 0 | √ |  |     |
| 70 | wmsid  |  |  | uniqueidentifier | 16 | 0 | √ |  |     |
| 71 | temp11  |  |  | nvarchar（50） | 100 | 0 | √ |  |     |
| 72 | temp12  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 73 | temp13  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 74 | temp14  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 75 | temp15  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 76 | temp16  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 77 | temp17  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 78 | temp18  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 79 | temp19  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 80 | temp20  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |





## scb_xsjlsheet
> 销售记录子表，记录主表订单的商品信息   
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | Number |  | √ | Decimal（18,0） | 9 | 0 |  |  |  主键  |
| 2 | father  |  |  | Decimal（18,0） | 9 | 0 | √ |  |  父级  |
| 3 | cpbh  |  |  | nvarchar(50) | 100 | 0 | √ |  |  产品编号  |
| 4 | cpgg  |  |  | nvarchar(255) | 510 | 0 | √ |  |  产品规格  |
| 5 | wxdj  |  |  | float | 8 | 0 |   |  |  销售价格  |
| 6 | sl  |  |  | int | 4 | 0 |   |  |  数量  |
| 7 | jldw  |  |  | nvarchar(50) | 100 | 0 | √ |  |  计量单位  |
| 8 | zje  |  |  | float | 8 | 0 | √ |  |  总金额  |
| 9 | bzcd  |  |  | float | 8 | 0 | √ |  |  包装长度  |
| 10 | bzkd  |  |  | float | 8 | 0 | √ |  |  包装宽度  |
| 11 | bzgd  |  |  | float | 8 | 0 | √ |  |  包装高度  |
| 12 | tj  |  |  | float | 8 | 0 | √ |  |  体积  |
| 13 | ztj  |  |  | float | 8 | 0 | √ |  |  总体积  |
| 14 | mz  |  |  | float | 8 | 0 | √ |  |  毛重  |
| 15 | zmz  |  |  | float | 8 | 0 | √ |  |  总毛重  |
| 16 | chck  |  |  | nvarchar(50) | 100 | 0 |   |  |  出货仓库  |
| 17 | kuw  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 18 | thstate  |  |  | int | 4 | 0 | √ |  |     |
| 19 | thje |  |  | float | 8 | 0 | √ |  |  退货金额  |
| 20 | thyf  |  |  | float | 8 | 0 | √ |  |  退货运费  |
| 21 | thsl  |  |  | int | 4 | 0 | √ |  |  退货数量  |
| 22 | thtime  |  |  | nvarchar(50) | 100 | 0 | √ |  |  退货时间  |
| 23 | state  |  |  | int | 4 | 0 | √ |  |     |
| 24 | Email  |  |  | nvarchar(300) | 600 | 0 | √ |  |  邮箱  |
| 25 | ptyf  |  |  | float | 8 | 0 | √ |  |  平台运费  |
| 26 | cptype  |  |  | int | 4 | 0 | √ |  |     |
| 27 | sku  |  |  | nvarchar(150) | 300 | 0 | √ |  |     |
| 28 | qtck  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 29 | qtsl  |  |  | int | 4 | 0 | √ |  |     |
| 30 | SalesTaxState  |  |  | float | 8 | 0 | √ |  |     |
| 31 | cbdj  |  |  | float | 8 | 0 |   |  |  成本单价  |
| 32 | itemno  |  |  | nvarchar(255) | 510 | 0 | √ |  |     |
| 33 | tracknofee  |  |  | float | 8 | 0 | √ |  |     |
| 34 | trackno  |  |  | nvarchar(255) | 510 | 0 | √ |  |  跟踪号  |
| 35 | Temp1  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 36 | Temp2  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 37 | Temp3  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 38 | Temp4  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 39 | Temp5  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 40 | _MASK_FROM_V2  |  |  | timestamp | 8 | 0 | √ |  |     |
| 41 | temp16  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 42 | temp17  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 43 | temp18  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 44 | temp19  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 45 | temp20  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |




## scb_xsjlpart
> 销售记录配件表，和销售主表根据OrderID关联   
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | number  |  | √ | Decimal（18,0） | 9 | 0 |  |  |  主键  |
| 2 | name  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 3 | depart  |  |  | nvarchar(50) | 100 | 0 | √ |  |  部门编号  |
| 4 | hthm  |  |  | nvarchar(200) | 400 | 0 | √ |  |  合同号码  |
| 5 | OrderId  |  |  | nvarchar(50) | 100 | 0 | √ |  |  订单编号  |
| 6 | newdateis  |  |  | datetime | 8 | 0 | √ |  |  订单下载时间(创建时间)     北京时间:下载时间  |
| 7 | moddateis  |  |  | datetime | 8 | 0 | √ |  |  订单修改时间(修改时间)     北京时间:最后一次被修改的时间  |
| 8 | orderdate  |  |  | datetime | 8 | 0 | √ |  |  订单时间  |
| 9 | fkzh  |  |  | nvarchar(50) | 100 | 0 | √ |  |  付款账号  |
| 10 | khname  |  |  | nvarchar(200) | 400 | 0 | √ |  |  客户姓名  |
| 11 | Address  |  |  | nvarchar(100) | 200 | 0 | √ |  |  客户地址  |
| 12 | lxfs  |  |  | varchar(50) | 50 | 0 | √ |  |  联系方式(电话)  |
| 13 | email  |  |  | varchar(300) | 300 | 0 | √ |  |  邮箱  |
| 14 | lywl  |  |  | varchar(50) | 50 | 0 | √ |  |  联系物流     Fba:货在亚马逊  |
| 15 | yf  |  |  | float | 8 | 0 | √ |  |  运费  |
| 16 | zhuizh  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 17 | xspt  |  |  | varchar(50) | 50 | 0 | √ |  |  销售平台  |
| 18 | country  |  |  | varchar(50) | 50 | 0 | √ |  |  国家代码  |
| 19 | dianpu  |  |  | varchar(50) | 50 | 0 | √ |  |  店铺  |
| 20 | chck  |  |  | varchar(50) | 50 | 0 | √ |  |  出货仓库  |
| 21 | fkfs  |  |  | varchar(30) | 30 | 0 | √ |  |  付款方式  |
| 22 | fhdate  |  |  | datetime | 8 | 0 | √ |  |     |
| 23 | zsl  |  |  | int | 4 | 0 | √ |  | 总数量  |
| 24 | xszje  |  |  | float | 8 | 0 | √ |  |  销售总资金  |
| 25 | yfzje  |  |  | float | 8 | 0 | √ |  |  Ebay价格  |
| 26 | rxszje  |  |  | float | 8 | 0 | √ |  |     |
| 27 | ryfzje  |  |  | float | 8 | 0 | √ |  |     |
| 28 | bz  |  |  | varchar(50) | 50 | 0 | √ |  |  备注  |
| 29 | state  |  |  | int | 4 | 0 | √ |  |  订单状态     6:取消订单     3：其他订单     4：暂停订单     0：正常订单     其他：未付款订单  |
| 30 | statsa  |  |  | nvarchar(50) | 100 | 0 | √ |  |  州     如：华盛顿州  |
| 31 | county  |  |  | nvarchar(50) | 100 | 0 | √ |  |  收货国家大区  |
| 32 | city  |  |  | nvarchar(50) | 100 | 0 | √ |  |  城市  |
| 33 | phone  |  |  | varchar(50) | 50 | 0 | √ |  |  电话  |
| 34 | others  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 35 | zip  |  |  | varchar(50) | 50 | 0 | √ |  |  邮编  |
| 36 | Address1  |  |  | nvarchar(100) | 200 | 0 | √ |  |     |
| 37 | Ebayzje  |  |  | float | 8 | 0 | √ |  |     |
| 38 | creattime  |  |  | datetime | 8 | 0 | √ |  |  创建时间  |
| 39 | recordnumber  |  |  | nvarchar(50) | 100 | 0 | √ |  |  记录号  |
| 40 | zek  |  |  | float | 8 | 0 | √ |  |  折扣跟退金金额  |
| 41 | thstate  |  |  | int | 4 | 0 | √ |  |  退货转态  |
| 42 | thsl  |  |  | int | 4 | 0 | √ |  |  退货数量  |
| 43 | thwpzt  |  |  | int | 4 | 0 | √ |  |     |
| 44 | Zsyf  |  |  | float | 8 | 0 | √ |  |     |
| 45 | sfzm  |  |  | tinyint | 8 | 0 | √ |  |  是否转卖给tpp  |
| 46 | isly  |  |  | tinyint | 8 | 0 | √ |  |     |
| 47 | lytime  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 48 | trackno  |  |  | nvarchar(500) | 1000 | 0 | √ |  |  跟踪号  |
| 49 | trackfee  |  |  | float | 8 | 0 | √ |  |     |
| 50 | logicswayoid  |  |  | int | 4 | 0 | √ |  |  物流方式Id  |
| 61 | warehouseid  |  |  | int | 4 | 0 | √ |  |  仓库id  |
| 62 | filterflag  |  |  | int | 4 | 0 | √ |  |  订单阶段     4：待处理订单(自动)     5：待处理订单     7：待发货订单     10：已打订单     11：全部订单  |
| 63 | servicetype  |  |  | nvarchar(500) | 1000 | 0 | √ |  |  物流  |
| 64 | signtype  |  |  | tinyint | 8 | 0 | √ |  |  标记类别     0：普通     1：次要     2：紧急  |
| 65 | Fromcountry  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 66 | ismark  |  |  | int | 4 | 0 | √ |  |  标记发货     1：已完成     3：已完成     2：已完成     0：未完成  |
| 67 | B2CSHIPMENT  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 68 | TEMP1  |  |  | nvarchar(50) | 100 | 0 | √ |  |  标记状态     1：已标记     3：已标记     0：未标记  |
| 69 | TEMP2  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 70 | TEMP3  |  |  | nvarchar(50) | 100 | 0 | √ |  |  1：退款退货     2:退款     3:退货补发     4:重发     5:补发配件     6:卡车运送     7:退货单     8:补发单     9:买家自提     其他:付款订单  |
| 71 | TEMP4  |  |  | nvarchar(50) | 100 | 0 | √ |  |  原始数量  |
| 72 | TEMP5  |  |  | nvarchar(50) | 100 | 0 | √ |  |  交易流水  |
| 73 | TEMP6  |  |  | nvarchar(50) | 100 | 0 | √ |  |  记录OrderSearch操作     1退款操作     2退货操作（hooya）     3退货操作（customer）     4补发操作     5退款退货（hooya）     6退款退货（customer）     7退款补发     8退货补发（hooya）     9退货补发（customer）     10退款退货补发（hooya）     11退款退货补发（customer） 12退货单     13补发单     14配件补发  |
| 74 | TEMP7  |  |  | nvarchar(50) | 100 | 0 | √ |  |  处理结果     1：完成     2：准备申请(自动处理状态，订单(如波兰投递站))  |
| 75 | TEMP8  |  |  | nvarchar(50) | 100 | 0 | √ |  |  打单日期  |
| 76 | TEMP9  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 77 | TEMP10  |  |  | nvarchar(50) | 100 | 0 | √ |  |  收货国家  |
| 78 | _MASK_FROM_V2  |  |  | timestamp | 8 | 0 |   |  |     |
| 79 | memo  |  |  | varchar(300) | 300 | 0 | √ |  |     |
| 80 | isUpload  |  |  | int | 4 | 0 |  |  |     |
| 81 | timeget  |  |  | datetime | 8 | 0 | √ |  |     |




## scb_xsjlpartsheet
>   销售记录配件详情表 
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | number |  | √ | int | 16 | 0 |  |  |  主键  |
| 2 | father  |  |  | int | 4 | 0 | √ |  |  父级  |
| 3 | cpbh  |  |  | nvarchar(100) | 200 | 0 | √ |  |  产品编号  |
| 4 | cpgg  |  |  | nvarchar(255) | 510 | 0 | √ |  |  产品规格  |
| 5 | wxdj  |  |  | float | 8 | 0 |   |  |  销售价格  |
| 6 | sl  |  |  | int | 4 | 0 |   |  |  数量  |
| 7 | jldw  |  |  | nvarchar(50) | 100 | 0 | √ |  |  计量单位  |
| 8 | zje  |  |  | float | 8 | 0 | √ |  |  总金额  |
| 9 | bzcd  |  |  | float | 8 | 0 | √ |  |  包装长度  |
| 10 | bzkd  |  |  | float | 8 | 0 | √ |  |  包装宽度  |
| 11 | bzgd  |  |  | float | 8 | 0 | √ |  |  包装高度  |
| 12 | tj  |  |  | float | 8 | 0 | √ |  |  体积  |
| 13 | ztj  |  |  | float | 8 | 0 | √ |  |  总体积  |
| 14 | mz  |  |  | float | 8 | 0 | √ |  |  毛重  |
| 15 | zmz  |  |  | float | 8 | 0 | √ |  |  总毛重  |
| 16 | chck  |  |  | nvarchar(50) | 100 | 0 |   |  |  出货仓库  |
| 17 | kuw  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 18 | thstate  |  |  | int | 4 | 0 | √ |  |     |
| 19 | thje |  |  | float | 8 | 0 | √ |  |  退货金额  |
| 20 | thyf  |  |  | float | 8 | 0 | √ |  |  退货运费  |
| 21 | thsl  |  |  | int | 4 | 0 | √ |  |  退货数量  |
| 22 | thtime  |  |  | nvarchar(50) | 100 | 0 | √ |  |  退货时间  |
| 23 | state  |  |  | int | 4 | 0 | √ |  |     |
| 24 | Email  |  |  | nvarchar(50) | 100 | 0 | √ |  |  邮箱  |
| 25 | ptyf  |  |  | float | 8 | 0 | √ |  |  平台运费  |
| 26 | cptype  |  |  | int | 4 | 0 | √ |  |     |
| 27 | sku  |  |  | nvarchar(150) | 300 | 0 | √ |  |     |
| 28 | qtck  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 29 | qtsl  |  |  | int | 4 | 0 | √ |  |     |
| 30 | SalesTaxState  |  |  | float | 8 | 0 | √ |  |     |
| 31 | cbdj  |  |  | float | 8 | 0 |   |  |  成本单价  |
| 32 | itemno  |  |  | nvarchar(255) | 510 | 0 | √ |  |     |
| 33 | tracknofee  |  |  | float | 8 | 0 | √ |  |     |
| 34 | trackno  |  |  | nvarchar(255) | 510 | 0 | √ |  |  跟踪号  |
| 35 | Temp1  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 36 | Temp2  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 37 | Temp3  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 38 | Temp4  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 39 | Temp5  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 40 | _MASK_FROM_V2  |  |  | timestamp | 8 | 0 |   |  |     |




## scbdianpu
> 店铺表   
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | id |  | √ | int | 16 | 0 |  |  |  主键  |
| 2 | dianpu  |  |  | varchar(50) | 50 | 0 | √ |  |  店铺名  |
| 3 | pingtai  |  |  | varchar(50) | 50 | 0 | √ |  |  平台  |
| 4 | jianc  |  |  | varchar(50) | 50 | 0 | √ |  |  简称  |
| 5 | charge  |  |  | float | 8 | 0 | √ |  |  收费  |
| 6 | adrate  |  |  | float | 8 | 0 | √ |  |     |
| 7 | otsdisply  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 8 | country  |  |  | varchar(50) | 50 | 0 | √ |  |  大区块分类     JP：日本     EU：欧盟     US：美国     AU：澳大利亚  |
| 9 | ischeck  |  |  | int | 4 | 0 | √ |  |     |
| 10 | inuse  |  |  | int | 4 | 0 | √ |  |     |
| 11 | realCountry  |  |  | varchar(50) | 50 | 0 | √ |  |  国家     国家代码  |
| 12 | junk  |  |  | int | 4 | 0 | √ |  |     |
| 13 | paypal  |  |  | int | 4 | 0 | √ |  |     |
| 14 | ClientId  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 15 | ClientSecret  |  |  | varchar(4000) | 4000 | 0 | √ |  |     |
| 16 | refresh_token  |  |  | varchar(500) | 500 | 0 | √ |  |     |
| 17 | pFlag  |  |  | nchar（1） | 2 | 0 | √ |  |     |
| 18 | curr  |  |  | nvarchar（10） | 20 | 0 | √ |  |     |
| 19 | charge_old  |  |  | float | 8 | 0 | √ |  |     |
| 20 | adrate_old  |  |  | float | 8 | 0 | √ |  |     |
| 21 | mainid  |  |  | int | 4 | 0 | √ |  |     |
| 21 | _MASK_FROM_V2  |  |  | timestamp | 8 | 0 |   |  |     |
| 22 | ns_xsjlother  |  |  | int | 4 | 0 | √ |  |     |





## scbck
> 仓库表   
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | number |  | √ | int | 4 | 0 |  |  |  主键，流水号  |
| 2 | id  |  |  | varchar(20) | 20 | 0 | √ |  |  对内ID  |
| 3 | name  |  |  | varchar(20) | 20 | 0 | √ |  |  仓库编号  |
| 4 | enid  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 5 | realname  |  |  | varchar(50) | 50 | 0 | √ |  |  仓库名称  |
| 6 | state  |  |  | int | 4 | 0 | √ |  |  仓库状态  |
| 7 | shippedservice  |  |  | nvarchar(50) | 100 | 0 | √ |  |  允许匹配的物流  |
| 8 | countryid  |  |  | nvarchar(50) | 100 | 0 | √ |  |  所属国家id  |
| 9 | pyservice  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 10 | storeid  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 11 | sequence  |  |  | int | 4 | 0 | √ |  |  东西部匹配顺序  |
| 12 | moneysign  |  |  | varchar(10) | 10 | 0 | √ |  |  货币  |
| 13 | _MASK_FROM_V2  |  |  | timestamp | 8 | 0 |   |  |  此表为同步表  |
| 14 | firstsend  |  |  | int | 4 | 0 | √ |  |  首发仓库  |
| 15 | groupname  |  |  | varchar(50) | 50 | 0 | √ |  |  仓库分组  |
| 16 | showstate  |  |  | int | 4 | 0 | √ |  |     |
| 17 | sort  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 18 | tuihuoid  |  |  | int | 4 | 0 | √ |  |     |
| 19 | AreaLocation  |  |  | nvarchar(50) | 100 | 0 | √ |  |  发货区域  |
| 20 | AreaSort  |  |  | int | 4 | 0 | √ |  |     |
| 21 | flag  |  |  | varchar(5) | 5 | 0 | √ |  |  仓库库位前缀  |
| 22 | Origin  |  |  | nvarchar(50) | 100 | 0 | √ |  |  prime订单发货区域编码  |
| 23 | OriginSort  |  |  | int | 4 | 0 | √ |  |  prime发货区域优先级  |
| 24 | IsMatching  |  |  | int | 4 | 0 | √ |  |     |
| 25 | IsThird  |  |  | int | 4 | 0 | √ |  |  是否第三方仓库  |
| 26 | countrybelong  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 27 | arealocationsort  |  |  | int | 4 | 0 | √ |  |     |
| 28 | IsDelivery  |  |  | char(1) | 1 | 0 | √ |  |     |
| 29 | IsReal  |  |  | char(1) | 1 | 0 | √ |  |  发货区域  |
| 30 | RealWarehouse  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 31 | MainID  |  |  | int | 4 | 0 | √ |  |  仓库库位前缀  |
| 32 | GroupId  |  |  | uniqueidentifier | 16 | 0 | √ |  |  prime订单发货区域编码  |
| 33 | short  |  |  | int | 4 | 0 | √ |  |  prime发货区域优先级  |
| 34 | grade  |  |  | int | 4 | 0 | √ |  |     |
| 35 | citys  |  |  | nvarchar(50) | 100 | 0 | √ |  |  是否第三方仓库  |
| 36 | countrys  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 37 | wh_type  |  |  | nvarchar(10) | 20 | 0 | √ |  |     |
| 38 | WMSVersion  |  |  | nvarchar(10) | 20 | 0 | √ |  |     |





## scb_kcjl_location
>   库位表 
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 16 | 0 |  |  |  主键,流水号  |
| 2 | Location  |  |  | nvarchar(50) | 100 | 0 | √ |  |  库位  |
| 3 | Warehouse  |  |  | nvarchar(50) | 100 | 0 | √ |  |  所属仓库  |
| 4 | checked  |  |  | int | 4 | 0 | √ |  |  拣货顺序=>发货  |
| 5 | Height  |  |  | float | 8 | 0 | √ |  |     |
| 6 | Type  |  |  | nvarchar(10) | 20 | 0 | √ |  |     |
| 7 | Disable  |  |  | int | 4 | 0 | √ |  |  是否启用 备注：与虚拟仓公用时，只有一个仓库是正常发货     0：正常发货     1：禁止发货  |
| 8 | Aisle  |  |  | int | 4 | 0 | √ |  |  高层底层通道     例子：1001     10：一层     01：01通道  |
| 9 | PutAwayDisable  |  |  | int | 4 | 0 | √ |  |     |
| 10 | _MASK_TO_V2  |  |  | binary(8) | 8 | 0 | √ |  |  此表为被同步表  |




## scb_kcjl_area
> 库存记录表   
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | number |  | √ | numeric(18,0) | 16 | 0 |  |  |  主键,流水号  |
| 2 | wxht  |  |  | varchar(50) | 50 | 0 | √ |  |  外销合同  |
| 3 | hthm  |  |  | varchar(50) | 50 | 0 | √ |  |  合同号码  |
| 4 | cpbh  |  |  | varchar(30) | 30 | 0 | √ |  |  产品编号  |
| 5 | twhh  |  |  | varchar(30) | 30 | 0 | √ |  |  曾用编号  |
| 6 | jhrq  |  |  | varchar(10) | 10 | 0 | √ |  |  进货日期  |
| 7 | tj  |  |  | float | 8 | 0 | √ |  |  体积  |
| 8 | jcrq  |  |  | varchar(50) | 50 | 0 | √ |  |  进仓日期  |
| 9 | mz  |  |  | float | 8 | 0 | √ |  |  毛重  |
| 10 | jz  |  |  | float | 8 | 0 | √ |  |  净重  |
| 11 | bzcd  |  |  | float | 8 | 0 | √ |  |  包装长度  |
| 12 | bzkd  |  |  | float | 8 | 0 | √ |  |  包装宽度  |
| 13 | bzgd  |  |  | float | 8 | 0 | √ |  |  包装高度  |
| 14 | wxrl  |  |  | int | 4 | 0 | √ |  |  箱率  |
| 15 | jldw  |  |  | varchar(10) | 10 | 0 | √ |  |  计量单位  |
| 16 | ywry  |  |  | varchar(30) | 30 | 0 | √ |  |  业务人员  |
| 17 | kcxs  |  |  | int | 4 | 0 | √ |  |  库存箱数  |
| 18 | basexs  |  |  | int | 4 | 0 | √ |  |     |
| 19 | fzchxs  |  |  | int | 4 | 0 | √ |  |     |
| 20 | ztj  |  |  | float | 8 | 0 | √ |  |  总体积  |
| 21 | zmz  |  |  | float | 8 | 0 | √ |  |  总毛重  |
| 22 | zjz  |  |  | float | 8 | 0 | √ |  |  总净重  |
| 23 | cgdj  |  |  | float | 8 | 0 | √ |  |     |
| 24 | zje  |  |  | float | 8 | 0 | √ |  |     |
| 25 | cbdj  |  |  | float | 8 | 0 | √ |  |  成本单价  |
| 26 | cbzje  |  |  | float | 8 | 0 | √ |  |  成本总金额  |
| 27 | jwcb  |  |  | float | 8 | 0 | √ |  |  境外成本  |
| 28 | jwzcb  |  |  | float | 8 | 0 | √ |  |  境外总成本  |
| 29 | ywpm  |  |  | varchar(250) | 250 | 0 | √ |  |  英文品名  |
| 30 | cangw  |  |  | varchar(20) | 20 | 0 | √ |  |     |
| 31 | guiw  |  |  | varchar(10) | 10 | 0 | √ |  |     |
| 32 | material  |  |  | varchar(255) | 255 | 0 | √ |  |     |
| 33 | ypxs  |  |  | int | 4 | 0 | √ |  |     |
| 34 | chxs  |  |  | int | 4 | 0 | √ |  |  出货箱数  |
| 35 | zxl  |  |  | int | 4 | 0 | √ |  |  装箱率  |
| 36 | cyrq  |  |  | varchar(10) | 10 | 0 | √ |  |  出运日期  |
| 37 | state  |  |  | int | 4 | 0 | √ |  |     |
| 38 | place  |  |  | varchar(50) | 50 | 0 | √ |  |  仓库号  |
| 39 | bsxs  |  |  | int | 4 | 0 | √ |  |     |
| 40 | ychxs  |  |  | int | 4 | 0 | √ |  |     |
| 41 | bl  |  |  | float | 8 | 0 | √ |  |     |
| 42 | sku  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 43 | dgtj  |  |  | float | 8 | 0 | √ |  |     |
| 44 | thxs  |  |  | int | 4 | 0 | √ |  |     |   退货箱数
| 45 | bhxs  |  |  | int | 4 | 0 | √ |  |     |
| 46 | zcsl  |  |  | int | 4 | 0 | √ |  |  转出数量  |
| 47 | zrkc  |  |  | int | 4 | 0 | √ |  |  转入库存  |
| 48 | country  |  |  | varchar(20) | 20 | 0 | √ |  |  国家  |
| 49 | tjrksl  |  |  | int | 4 | 0 | √ |  |  退件入库数量  |
| 50 | tjxssl  |  |  | int | 4 | 0 | √ |  |  退件销售数量  |
| 51 | MASK_FROM_V2  |  |  | timestamp | 8 | 0 |   |  |     |




## scb_kcjl_area_cangw
>   库存表
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | id |  | √ | int | 16 | 0 |  |  |  主键，流水号  |
| 2 | father  |  |  | int | 4 | 0 | √ |  |     |
| 3 | cangw  |  |  | nvarchar(50) | 100 | 0 | √ |  |  库位  |
| 4 | sl  |  |  | int | 4 | 0 | √ |  |  数量  |
| 5 | cksl  |  |  | int | 4 | 0 | √ |  |     |
| 6 | cpbh  |  |  | nvarchar(50) | 100 | 0 | √ |  |  产品编号  |
| 7 | Location  |  |  | nvarchar(50) | 100 | 0 | √ |  |  仓库  |
| 8 | state  |  |  | int | 4 | 0 | √ |  |     |
| 9 | _MASK_TO_V2  |  |  | binary(8) | 8 | 0 | √ |  |     |
| 10 | IncomingTime  |  |  | nvarchar(200) | 400 | 0 | √ |  |     |




## scb_jckgl_area
>  入库表
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | Number |  | √ | numeric(18,0) | 9 | 0 |  |  |  主键  |
| 2 | Name  |  |  | varchar(30) | 30 | 0 | √ |  |     |
| 3 | depart  |  |  | varchar(255) | 255 | 0 | √ |  |     |
| 4 | newdateis  |  |  | varchar(30) | 30 | 0 | √ |  |  创建时间  |
| 5 | moddateis  |  |  | varchar(30) | 30 | 0 | √ |  |  修改时间  |
| 6 | jcbh  |  |  | varchar(50) | 50 | 0 | √ |  |  进仓编号  |
| 7 | hthm  |  |  | varchar(50) | 50 | 0 | √ |  |  合同号码  |
| 8 | sccj  |  |  | varchar(100) | 100 | 0 | √ |  |  生产厂家  |
| 9 | wxht  |  |  | varchar(50) | 50 | 0 | √ |  |  外销合同  |
| 10 | htrq  |  |  | varchar(20) | 20 | 0 | √ |  |  合同日期  |
| 11 | jhrq  |  |  | varchar(10) | 10 | 0 | √ |  |  进货日期  |
| 12 | khmc  |  |  | varchar(100) | 100 | 0 | √ |  |  客户名称  |
| 13 | ywry  |  |  | varchar(30) | 30 | 0 | √ |  |  业务人员  |
| 14 | cslxr  |  |  | varchar(50) | 50 | 0 | √ |  |  厂商联系人  |
| 15 | phone  |  |  | varchar(60) | 60 | 0 | √ |  |  手机号  |
| 16 | htzsl  |  |  | varchar(50) | 50 | 0 | √ |  |  合同总数量  |
| 17 | htzxs  |  |  | varchar(60) | 60 | 0 | √ |  |  合同总箱数  |
| 18 | htzmz  |  |  | float | 8 | 0 | √ |  |  合同总毛重  |
| 19 | htzjz  |  |  | float | 8 | 0 | √ |  |  合同总净重  |
| 20 | htztj  |  |  | float | 8 | 0 | √ |  |  合同总体积  |
| 21 | yhry  |  |  | varchar(30) | 30 | 0 | √ |  |  验货人员  |
| 22 | jcsj  |  |  | varchar(30) | 30 | 0 | √ |  |     |
| 23 | cangw  |  |  | varchar(20) | 20 | 0 | √ |  |     |
| 24 | jcdid  |  |  | int | 4 | 0 | √ |  |  进仓订单  |
| 25 | place  |  |  | varchar(30) | 30 | 0 | √ |  |  仓库号  |
| 26 | cs_id  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 27 | ischanged  |  |  | smallint | 2 | 0 | √ |  |     |
| 28 | feiyong  |  |  | float | 8 | 0 | √ |  |  费用  |
| 29 | ckgfy  |  |  | float | 8 | 0 | √ |  |  仓库管理费用  |
| 30 | hyfy  |  |  | float | 8 | 0 | √ |  |  货运费用  |
| 31 | qgfy  |  |  | float | 8 | 0 | √ |  |  清关费用  |
| 32 | llfy  |  |  | float | 8 | 0 | √ |  |  陆运费用  |
| 33 | gsfy  |  |  | float | 8 | 0 | √ |  |  关税  |
| 34 | ccfy  |  |  | float | 8 | 0 | √ |  |  存储费用  |
| 35 | othersfy  |  |  | float | 8 | 0 | √ |  |  其他费用  |
| 36 | hl  |  |  | float | 8 | 0 | √ |  |  汇率  |
| 37 | bxfy  |  |  | float | 8 | 0 | √ |  |  报销费用  |
| 38 | zcck  |  |  | varchar(50) | 50 | 0 | √ |  |  转出仓库  |
| 39 | zlck  |  |  | varchar(50) | 50 | 0 | √ |  |  转入仓库  |
| 40 | Kczt  |  |  | int | 4 | 0 | √ |  |  是否可以操作     1：禁止     0：开放权限  |
| 41 | country  |  |  | varchar(30) | 30 | 0 | √ |  |  国家  |
| 42 | VirtualWarehouse  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 43 | UnloadingType  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 44 | Pallets  |  |  | int | 4 | 0 |   |  |     |
| 45 | ns_time  |  |  | datetime | 8 | 0 | √ |  |     |
| 46 | _MASK_FROM_B2  |  |  | timestamp | 8 | 0 |   |  |     |
| 47 | shipmentid  |  |  | nvarchar(50) | 100 | 0 |   |  |     |




## scb_jcglsheet_area
>  移库子表  
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | Number |  | √ | numeric(18,0) | 9 | 0 |  |  |  主键，流水号  |
| 2 | father  |  |  | int | 4 | 0 | √ |  |  主表  |
| 3 | cpbh  |  |  | varchar(30) | 30 | 0 | √ |  |  产品编号  |
| 4 | twhh  |  |  | varchar(30) | 30 | 0 | √ |  |  曾用编号  |
| 5 | cpgg  |  |  | varchar(20) | 20 | 0 | √ |  |  产品规格  |
| 6 | pzms  |  |  | text | 16 | 0 | √ |  |  产品描述  |
| 7 | gchh  |  |  | varchar(20) | 20 | 0 | √ |  |  工厂货号  |
| 8 | zwpm  |  |  | varchar(50) | 50 | 0 | √ |  |  中文品名  |
| 9 | cgxs  |  |  | int | 4 | 0 | √ |  |  采购箱数  |
| 10 | bzdw  |  |  | varchar(10) | 10 | 0 | √ |  |  包装单位  |
| 11 | wxrl  |  |  | int | 4 | 0 | √ |  |  箱率  |
| 12 | cgsl  |  |  | int | 4 | 0 | √ |  |  采购数量  |
| 13 | jldw  |  |  | varchar(10) | 10 | 0 | √ |  |  计量单位  |
| 14 | mz  |  |  | float | 8 | 0 | √ |  |  毛重  |
| 15 | zmz  |  |  | float | 8 | 0 | √ |  |  总毛重  |
| 16 | jz  |  |  | float | 8 | 0 | √ |  |  净重  |
| 17 | zjz  |  |  | float | 8 | 0 | √ |  |  总净重  |
| 18 | bzcd  |  |  | float | 8 | 0 | √ |  |  包装长度  |
| 19 | bzkd  |  |  | float | 8 | 0 | √ |  |  包装宽度  |
| 20 | bzgd  |  |  | float | 8 | 0 | √ |  |  包装高度  |
| 21 | tj  |  |  | float | 8 | 0 | √ |  |  体积      |
| 22 | ztj  |  |  | float | 8 | 0 | √ |  |  总体积  |
| 23 | chsl  |  |  | int | 4 | 0 | √ |  |  出货数量  |
| 24 | sysl  |  |  | int | 4 | 0 | √ |  |  剩余数量  |
| 25 | chxs  |  |  | int | 4 | 0 | √ |  |  出货箱数  |
| 26 | syxs  |  |  | int | 4 | 0 | √ |  |  剩余箱数  |
| 27 | rowno  |  |  | int | 4 | 0 | √ |  |     |
| 28 | baoz  |  |  | varchar(20) | 20 | 0 | √ |  |     |
| 29 | kuans  |  |  | varchar(20) | 20 | 0 | √ |  |     |
| 30 | pinz  |  |  | varchar(20) | 20 | 0 | √ |  |     |
| 31 | huoy  |  |  | varchar(10) | 10 | 0 | √ |  |     |
| 32 | pais  |  |  | varchar(10) | 10 | 0 | √ |  |     |
| 33 | kaix  |  |  | int | 4 | 0 | √ |  |     |
| 34 | quy  |  |  | int | 4 | 0 | √ |  |     |
| 35 | guiw  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 36 | memo  |  |  | text | 16 | 0 | √ |  |     |
| 37 | bl1  |  |  | float | 8 | 0 | √ |  |     |
| 38 | cbdj  |  |  | float | 8 | 0 | √ |  |  成本单价  |
| 39 | cbzje  |  |  | float | 8 | 0 | √ |  |  成本总金额  |
| 40 | jwcb  |  |  | float | 8 | 0 | √ |  |  境外成本  |
| 41 | jwzcb  |  |  | float | 8 | 0 | √ |  |  境外总成本  |
| 42 | wxdj  |  |  | float | 8 | 0 | √ |  |  外销单价  |
| 43 | kcdj  |  |  | float | 8 | 0 | √ |  |  库存单价  |
| 44 | kcsl  |  |  | int | 4 | 0 | √ |  |  库存数量  |
| 45 | price  |  |  | float | 8 | 0 | √ |  |  价格  |
| 46 | _MASK_FROM_V2  |  |  | timestamp | 8 | 0 |   |  |     |




## scb_Logisticsmode
>  物流表  
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | oid |  | √ | int | 16 | 0 |  |  |  主键  |
| 2 | operatoren  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 3 | operatorcn  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 4 | logisticsmode  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 5 | specificlogistics  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 6 | autotrackingno  |  |  | int | 4 | 0 | √ |  |     |
| 7 | servicecode  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 8 | automatchorder  |  |  | int | 4 | 0 | √ |  |     |
| 9 | sorting  |  |  | int | 4 | 0 | √ |  |     |
| 10 | logisticscompany  |  |  | nvarchar(150) | 300 | 0 | √ |  |     |
| 11 | remark  |  |  | nvarchar(1000) | 2000 | 0 | √ |  |     |
| 12 | checkeds  |  |  | int | 4 | 0 | √ |  |     |
| 13 | warehoues  |  |  | nvarchar(500) | 1000 | 0 | √ |  |     |
| 14 | IsEnable  |  |  | int | 4 | 0 | √ |  |     |
| 15 | Label_Suffix  |  |  | nvarchar(20) | 40 | 0 | √ |  |     |
| 16 | Invoice_Suffix  |  |  | nvarchar(20) | 40 | 0 | √ |  |     |
| 17 | Countries  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 18 | logisticsName  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 19 | _MASK_FROM_V2  |  |  | timestamp | 8 | 0 | √ |  |     |




## scb_pickinglist_state_completed
>  物流跟踪主表 
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | Bigint | 8 | 0 |  |  |  主键  |
| 2 | Date  |  |  | datetime | 8 | 0 |   |  |  日期  |
| 3 | Father  |  |  | int | 4 | 0 | √ |  |  NULL  |
| 4 | Type  |  |  | nvarchar(200) | 400 | 0 | √ |  |  物流公司  |
| 5 | Des  |  |  | nvarchar(200) | 400 | 0 | √ |  |  描述  |
| 6 | ProcessDate  |  |  | nvarchar(50) | 100 | 0 | √ |  |  处理日期  |
| 7 | Nid  |  |  | Bigint | 8 | 0 | √ |  |  主表number     Scb_xsjl表的number  |
| 8 | TrackNo  |  |  | nvarchar(200) | 400 | 0 | √ |  |  追踪号  |
| 9 | Timeget  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 10 | Warehouse  |  |  | nvarchar(50) | 100 | 0 | √ |  |  仓库名  |
| 11 | Filterflag  |  |  | nvarchar(10) | 100 | 0 | √ |  |  状态  |
| 12 | SyncID  |  |  | nvarchar(36) | 100 | 0 | √ |  |     |
| 13 | _MASK_FROM_V2  |  |  | timestamp | 8 | 0 | √ |  |     |
| 14 | ProcessCode  |  |  | int | 4 | 0 | √ |  |     |
| 15 | AddressCorrected  |  |  | int | 4 | 0 | √ |  |     |




## scb_order_carrier
>  订单物流匹配表，本地仓库物流一键匹配，新规则表
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | decimal(18,0) | 9 | 0 |  |  |  主键  |
| 2 | orderid  |  |  | nvarchar(50) | 100 | 0 |   |  |    |
| 3 | oid  |  |  | int | 4 | 0 | √ |  |  NULL  |
| 4 | servicetype  |  |  | nvarchar(50) | 100 | 0 | √ |  |    |
| 5 | shipservice  |  |  | nvarchar( varchar(25) | 25 0 | √ |  |    |
| 6 | dianpu  |  |  | nvarchar(50) | 100 | 0 | √ |  |    |
| 7 | temp  |  |  | nvarchar(50) | 100 | 0 | √ |  |    |
| 8 | temp1  |  |  | nvarchar(50) | 100 | 0 | √ |  |    |
| 9 | temp2  |  |  | nvarchar(200) | 400 | 0 | √ |  |     |




## scb_pickinglist_state_checksheet
>  物流跟踪细节表  
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | Number |  | √ | numeric | 16 | 0 |  |  |  主键，流水ID  |
| 2 | father  |  |  | numeric | 100 | 0 | √ |  |     |
| 3 | newdateis  |  |  | datetime | 8 | 0 | √ |  |  创建时间  |
| 4 | trackdetail  |  |  | varchar | 100 | 0 | √ |  |  细节说明  |
| 5 | logisticstype  |  |  | nvarchar | 100 | 0 | √ |  |  物流种类  |
| 6 | nid  |  |  | bigint | 8 | 0 | √ |  |  主表Number     Scb_xsjl表的number  |
| 7 | trackno  |  |  | nvarchar | 100 | 0 | √ |  |  跟踪号  |
| 8 | syncID  |  |  | nvarchar(36) | 100 | 0 | √ |  |     |
| 9 | nodeDes  |  |  | nvarchar(100) | 200 | 0 | √ |  |     |
| 10 |  _MASK_FROM_V2  |  |  | timestamp | 8 | 0 |   |  |     |
| 11 | ProcessCode  |  |  | int | 4 | 0 | √ |  |     |




## N_goods
>  商品表  
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 16 | 0 |  |  |  主键  |
| 2 | Name  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 3 | Depart  |  |  | varchar(50) | 50 | 0 | √ |  |  部门ID  |
| 4 | Newdate  |  |  | datetime | 8 | 0 | √ |  |  下载时间(创建时间)     北京时间  |
| 5 | Moddate  |  |  | datetime | 8 | 0 | √ |  |  修改时间(修改时间)     北京时间:最后一次被修改的时间  |
| 6 | Cpbh  |  |  | varchar(50) | 50 | 0 | √ |  |  产品编号  |
| 7 | Sku  |  |  | varchar(50) | 50 | 0 | √ |  |  对应Sku  |
| 8 | Storename  |  varchar(250)  | nvarchar | 100 | 0 | √ |  |     ||
| 9 | Goodsname  |  |  | varchar(300) | 300 | 0 | √ |  |  商品名称  |
| 10 | Unit  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 11 | Costprice  |  |  | float | 8 | 0 | √ |  |  成本价格  |
| 12 | Weight  |  |  | float | 8 | 0 | √ |  |  重量  |
| 13 | Saleprice  |  |  | float | 8 | 0 | √ |  |  成品价格  |
| 14 | Quantity  |  |  | int | 4 | 0 | √ |  |  数量  |
| 15 | Maxnum  |  |  | int | 4 | 0 | √ |  |  最大值  |
| 16 | Minnum  |  |  | int | 4 | 0 | √ |  |  最小值  |
| 17 | goodsstatus  |  |  | varchar(50) | 50 | 0 | √ |  |  商品状态  |
| 18 | stockMinAmount  |  |  | int | 4 | 0 | √ |  |  库存最小值  |
| 19 | stockMaxAmount  |  |  | int | 4 | 0 | √ |  |  库存最大值  |
| 20 | saler  |  |  | varchar(50) | 50 | 0 | √ |  |  销售人员  |
| 21 | cgry  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 22 | groupflag  |  |  | tinyint | 8 | 0 | √ |  |  是否存在组合产品     0：不存在     1：存在  |
| 23 | country  |  |  | varchar(50) | 50 | 0 | √ |  |  国家  |
| 24 | wlfs  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 25 | type  |  |  | varchar(200) | 200 | 0 | √ |  |     |
| 26 | bzcd  |  |  | float | 8 | 0 | √ |  |  包装长度  |
| 27 | bzkd  |  |  | float | 8 | 0 | √ |  |  包装宽度  |
| 28 | bzgd  |  |  | float | 8 | 0 | √ |  |  包装高度  |
| 29 | bzunit  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 30 | state  |  |  | int | 4 | 0 | √ |  |     |
| 31 | package  |  |  | int | 4 | 0 | √ |  |     |
| 32 | grade  |  |  | varchar(10) | 10 | 0 | √ |  |     |
| 33 | rank  |  |  | int | 4 | 0 | √ |  |     |
| 34 | costpricel  |  |  | float | 8 | 0 | √ |  |     |
| 35 | IsMaintaValue  |  |  | bit | 1 | 0 |   |  |     |
| 36 | _mask_From_v2  |  |  | timestamp | 8 | 0 |   |  |  此表为同步表  |




## CP
>  产品表  
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | number |  | √ | numeric(18, 0) | 16 | 0 |  |  |  主键  |
| 2 | name  |  |  | varchar(30) | 30 | 0 | √ |  |     |
| 3 | depart  |  |  | varchar(255) | 255 | 0 | √ |  |  部门ID  |
| 4 | newdateis  |  |  | varchar(30) | 30 | 0 | √ |  |  下载时间(创建时间)     北京时间  |
| 5 | moddateis  |  |  | varchar(30) | 30 | 0 | √ |  |  修改时间(修改时间)     北京时间:最后一次被修改的时间  |
| 6 | cpbh  |  |  | varchar(20) | 20 | 0 | √ |  |  产品编号  |
| 7 | cpgg  |  |  | varchar(100) | 100 | 0 | √ |  |  产品规格  |
| 8 | zwpm  |  |  | varchar(50) | 50 | 0 | √ |  |  中文品名  |
| 9 | hgbm  |  |  | varchar(10) | 10 | 0 | √ |  |  海关编码  |
| 10 | tsl  |  |  | float | 8 | 0 | √ |  |  退税率  |
| 11 | jldw  |  |  | varchar(10) | 10 | 0 | √ |  |  计量单位  |
| 12 | bzdw  |  |  | varchar(10) | 10 | 0 | √ |  |  包装单位  |
| 13 | bzrl  |  |  | int | 4 | 0 | √ |  |     |
| 14 | bzcd  |  |  | float | 8 | 0 | √ |  |  包装长度  |
| 15 | bzkd  |  |  | float | 8 | 0 | √ |  |  包装宽度  |
| 16 | bzgd  |  |  | float | 8 | 0 | √ |  |  包装高度  |
| 17 | cpsm  |  |  | float | 8 | 0 | √ |  |     |
| 18 | mxmz  |  |  | float | 8 | 0 | √ |  |     |
| 19 | mxjz  |  |  | float | 8 | 0 | √ |  |     |
| 20 | bztj  |  |  | float | 8 | 0 | √ |  |  包装体积  |
| 21 | cplx  |  |  | varchar(30) | 30 | 0 | √ |  |  产品类型  |
| 22 | hbdm  |  |  | varchar(10) | 10 | 0 | √ |  |  货币代码  |
| 23 | cgdj  |  |  | float | 8 | 0 | √ |  |     |
| 24 | shm  |  |  | text | 16 | 0 | √ |  |     |
| 25 | english_shm  |  |  | text | 16 | 0 | √ |  |     |
| 26 | ywpm  |  |  | varchar(250) | 250 | 0 | √ |  |  英文品名  |
| 27 | qckc  |  |  | int | 4 | 0 | √ |  |     |
| 28 | ljrk  |  |  | int | 4 | 0 | √ |  |     |
| 29 | jlck  |  |  | int | 4 | 0 | √ |  |     |
| 30 | cksl  |  |  | int | 4 | 0 | √ |  |     |
| 31 | ccwz  |  |  | varchar(60) | 60 | 0 | √ |  |     |
| 32 | txmh  |  |  | varchar(30) | 30 | 0 | √ |  |  条形码  |
| 33 | pzms  |  |  | text | 16 | 0 | √ |  |  产品描述  |
| 34 | twhh  |  |  | nvarchar(70) | 140 | 0 | √ |  |  曾用编号  |
| 35 | jdrq  |  |  | varchar(30) | 30 | 0 | √ |  |  建档日期  |
| 36 | sfxp  |  |  | varchar(4) | 4 | 0 | √ |  |     |
| 37 | cptm  |  |  | varchar(8) | 8 | 0 | √ |  |  产品条码  |
| 38 | hjbh  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 39 | disp  |  |  | tinyint | 8 | 0 | √ |  |     |
| 40 | sfpp  |  |  | tinyint | 8 | 0 | √ |  |     |
| 41 | tuij  |  |  | tinyint | 8 | 0 | √ |  |     |
| 42 | cert  |  |  | varchar(200) | 200 | 0 | √ |  |     |
| 43 | khmc  |  |  | varchar(50) | 50 | 0 | √ |  |  客户名称  |
| 44 | more  |  |  | int | 4 | 0 | √ |  |     |
| 45 | ywms  |  |  | text | 16 | 0 | √ |  |  英文描述  |
| 46 | name2  |  |  | varchar(30) | 30 | 0 | √ |  |     |
| 47 | isnew  |  |  | tinyint | 8 | 0 | √ |  |     |
| 48 | qrrq  |  |  | datetime | 8 | 0 | √ |  |     |
| 49 | qrname  |  |  | varchar(30) | 30 | 0 | √ |  |     |
| 50 | newdepart  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 51 | operators  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 52 | cpms  |  |  | text | 16 | 0 | √ |  |     |
| 53 | cd  |  |  | float | 8 | 0 | √ |  |  长度  |
| 54 | kd  |  |  | float | 8 | 0 | √ |  |  宽度  |
| 55 | gd  |  |  | float | 8 | 0 | √ |  |  高度  |
| 56 | guil  |  |  | float | 8 | 0 | √ |  |     |
| 57 | bztj1  |  |  | float | 8 | 0 | √ |  |     |
| 58 | sku  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 59 | kfy  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 60 | saler  |  |  | varchar(50) | 50 | 0 | √ |  |  销售人员  |
| 61 | wxtm  |  |  | varchar(15) | 100 | 0 | √ |  |     |
| 62 | bzts  |  |  | int | 4 | 0 | √ |  |     |
| 63 | similarcp  |  |  | varchar(255) | 255 | 0 | √ |  |     |
| 64 | cptype  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 65 | isseason  |  |  | tinyint | 8 | 0 | √ |  |     |
| 66 | ldcsxx  |  |  | varchar(250) | 250 | 0 | √ |  |     |
| 67 | qryjz  |  |  | float | 8 | 0 | √ |  |     |
| 68 | qrymz  |  |  | float | 8 | 0 | √ |  |     |
| 69 | logo  |  |  | nvarchar(200) | 400 | 0 | √ |  |     |
| 70 | qrymjz  |  |  | bit | 1 | 0 | √ |  |     |
| 71 | cpmd  |  |  | nvarchar( varchar(25) | 25 0 | √ |  |     |
| 72 | qrsxgd  |  |  | bit | 1 | 0 | √ |  |     |
| 73 | sxgd  |  |  | float | 8 | 0 | √ |  |     |
| 74 | cz  |  |  | nvarchar(200) | 400 | 0 | √ |  |     |
| 75 | sfkfp  |  |  | bit | 1 | 0 | √ |  |     |
| 76 | bgms  |  |  | text | 16 | 0 | √ |  |     |
| 77 | xb  |  |  | int | 4 | 0 | √ |  |     |
| 78 | ys  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 79 | pl  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 80 | cm  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 81 | zbh  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 82 | _MASK_FROM_V2  |  |  | timestamp | 8 | 0 |   |  |     |
| 83 | isNewKind  |  |  | bit | 1 | 0 | √ |  |     |
| 84 | ns_time  |  |  | datetime | 8 | 0 | √ |  |     |
| 85 | isCostway  |  |  | bit | 1 | 0 | √ |  |     |
| 86 | HasTray  |  |  | bit | 1 | 0 | √ |  |     |
| 87 | UnifiedNo  |  |  | varchar(25) | 25 | 0 | √ |  |     |
| 88 | noCarton  |  |  | bit | 1 | 0 | √ |  |     |
| 89 | special_shaped  |  |  | float | 8 | 0 | √ |  |     |
| 90 | special_length  |  |  | float | 8 | 0 | √ |  |     |
| 91 | special_width  |  |  | float | 8 | 0 | √ |  |     |
| 92 | special_height_longer  |  |  | float | 8 | 0 | √ |  |     |
| 93 | special_height_shorter  |  |  | float | 8 | 0 | √ |  |     |



## scb_ups_zip
>  邮编表  
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 16 | 0 |  |  |  主键  |
| 2 | Origin  |  |  | varchar(50) | 50 | 0 | √ |  |  仓库邮编  |
| 3 | DestZip  |  |  | varchar(50) | 50 | 0 | √ |  |  具体邮编  |
| 4 | State  |  |  | varchar(50) | 50 | 0 | √ |  |  州  |
| 5 | GNDZone  |  |  | int | 4 | 0 | √ |  |  花费  |
| 6 | TNTDAYS  |  |  | int | 4 | 0 | √ |  |  时间  |
| 7 | IsValid  |  |  | int | 4 | 0 | √ |  |  是否有效  |
| 8 | _MASK_FROM_V2  |  |  | timestamp | 8 | 0 |   |  |     |




## scb_Errorlog
>    全局Error表
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 16 | 0 |  |  |  主键  |
| 2 | Message  |  |  | Varchar(2000) | 2000 | 0 | √ |  |  信息  |
| 3 | Stacktrack  |  |  | Varchar(5000) | 5000 | 0 | √ |  |  追踪  |
| 4 | Note  |  |  | Varchar(2000) | 2000 | 0 | √ |  |     |
| 5 | Creator  |  |  | varchar(50) | 50 | 0 | √ |  |  操作人  |
| 6 | Created  |  |  | datetime | 8 | 0 | √ |  |  操作时间  |




## scb_oplog
>    操作日志表，某订单在Eshop的流程操作表
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 16 | 0 |  |  |  主键  |
| 2 | Model  |  |  | Varchar(50) | 50 | 0 |   |  |  模块  |
| 3 | Type  |  |  | Varchar(50) | 50 | 0 |   |  |  执行操作  |
| 4 | Msg  |  |  | Varchar(500) | 500 | 0 |   |  |  信息  |
| 5 | Creator  |  |  | Varchar(50) | 50 | 0 |   |  |  操作人员  |
| 6 | Created  |  |  | datetime | 8 | 0 |   |  |  操作时间  |
| 7 | Des  |  |  | Varchar(500) | 100 | 0 | √ |  |     |




## scb_eshop_logs
>    订单相关Eshop表
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 16 | 0 |  |  |  主键  |
| 2 | Eslogs  |  |  | nvarchar(500) | 1000 | 0 | √ |  |  日志表述  |
| 3 | Types  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 4 | Nid  |  |  | nvarchar(50) | 100 | 0 | √ |  |  销售记录表ID  |




## v_cdc_xsjl
>    订单相关操作历史日志视图，与scb_xsjl表关联
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | Updatetime |  | √ | datetime | 16 | 0 |  |  |  更新时间  |
| 2 | _$operation  |  |  | nvarchar | 100 | 0 | √ |  |  操作指令  |




## v_cdc_xsjlsheet
>    订单子表相关操作历史日志表
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | Updatetime |  | √ | datetime | 16 | 0 |  |  |  更新时间  |
| 2 | _$operation  |  |  | nvarchar | 100 | 0 | √ |  |  操作指令  |




## scb_WMS_OrderLog
>    WMS自动处理日志表
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 16 | 0 |  |  |  主键  |
| 2 | logs  |  |  | nvarchar(4000) | 8000 | 0 |   |  |     |
| 3 | NID  |  |  | decimal(18,0) | 9 | 0 |   |  |     |
| 4 | LogType  |  |  | nvarchar(100) | 200 | 0 |   |  |     |
| 5 | Created  |  |  | datetime | 8 | 0 |   |  |     |
| 6 | CreateUser  |  |  | nvarchar(100) | 200 | 0 |   |  |     |



## scb_WMS_OrderResult
>    WMS自动处理结果表
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 16 | 0 |  |  |  主键  |
| 2 | number  |  |  | decimal(18,0) | 9 | 0 |   |  |     |
| 3 | count  |  |  | int | 4 | 0 |   |  |    |
| 4 | temp7  |  |  | nvarchar(10) | 20 | 0 | √ |  |    |
| 5 | filterflag  |  |  | int | 4 | 0 | √ |  |    |
| 6 | shipmentId  |  |  | nvarchar(100) | 200 | 0 | √ |  |    |
| 7 | Trackingnumber  |  |  | nvarchar(50) | 100 | 0 | √ |  |    |
| 8 | step  |  |  | int | 4 | 0 | √ |  |     |
| 9 | zsyf  |  |  | float | 8 | 0 | √ |  |     |
| 10 | IsFinsh  |  |  | bit | 8 | 0 |   |  |     |
| 11 | logicswayoid  |  |  | int | 4 | 0 | √ |  |     |
| 12 | servicetype  |  |  | nvarchar(100) | 200 | 0 | √ |  |     |
| 13 | Msg  |  |  | nvarchar(4000) | 8000 | 0 | √ |  |    |




## scb_kcjl_opterlog
>    库存记录操作日志表
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | uniqueidentifier | 16 | 0 |  |  |  主键  |
| 2 | OpterDatetime  |  |  | datetime | 8 | 0 |   |  |     |
| 3 | cpbh  |  |  | varchar(50) | 50 | 0 |   |  |    |
| 4 | oldsl  |  |  | int | 4 | 0 | √ |  |    |
| 5 | Sl  |  |  | int | 4 | 0 | √ |  |    |
| 6 | Location  |  |  | varchar(50) | 50 | 0 | √ |  |    |
| 7 | state  |  |  | int | 4 | 0 | √ |  |    |
| 8 | oldLocation  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 9 | _MASK_TO_V2  |  |  | binary(8) | 8 | 0 | √ |  |     |
| 10 | typemac  |  |  | nvarchar(500) | 8 | 0 |   |  |     |
| 11 | whoid  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |



## scb_pickinglist
>    拣货单-美国
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 16 | 0 |  |  |  主键  |
| 2 | NID  |  |  | int | 4 | 0 | √ |  |  订单储值号  |
| 3 | Cpbh  |  |  | nvarchar(50) | 100 | 0 | √ |  |  产品编号  |
| 4 | SKU  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 5 | QTY  |  |  | int | 4 | 0 | √ |  |  拣货数量  |
| 6 | Location  |  |  | varchar(1000) | 1000 | 0 | √ |  |  库位  |
| 7 | SN  |  |  | int | 4 | 0 | √ |  |  状态     -6：取消捡货     -5：取消移库     -4：卡车运输或UPSInternational     -3：需要转仓的订单     -2：取消订单     -1：系统自动处理的订单     0：未处理     1：待拣货     2：准备移库     3：当前仓库无库存     4：卡车运输     5：捡货异常     6：锁定库位（暂时没用）     7：待移库（已生成移库指令）     8：已捡货     9：普通订单暂停发货  |
| 8 | timeget  |  |  | date | 3 | 0 | √ |  |  时间  |
| 9 | Checked  |  |  | int | 4 | 0 | √ |  |  捡货顺序  |
| 10 | Pages  |  |  | nvarchar(100) | 200 | 0 | √ |  |     |
| 11 | Shop  |  |  | nvarchar(100) | 200 | 0 | √ |  |  店铺  |
| 12 | Way  |  |  | nvarchar(100) | 200 | 0 | √ |  |  物流  |
| 13 | itemno  |  |  | nvarchar(100) | 200 | 0 | √ |  |     |
| 14 | cangku  |  |  | nvarchar(100) | 200 | 0 | √ |  |  仓库名  |
| 15 | newdateis  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 16 | Aisle  |  |  | int | 4 | 0 | √ |  |     |




## scb_pickinglist_gb
>    拣货单-欧盟
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 16 | 0 |  |  |  主键  |
| 2 | NID  |  |  | int | 4 | 0 | √ |  |  订单储值号  |
| 3 | Cpbh  |  |  | nvarchar(50) | 100 | 0 | √ |  |  产品编号  |
| 4 | SKU  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 5 | QTY  |  |  | int | 4 | 0 | √ |  |  拣货数量  |
| 6 | Location  |  |  | varchar(1000) | 1000 | 0 | √ |  |  库位  |
| 7 | SN  |  |  | int | 4 | 0 | √ |  |  状态     -6：取消捡货     -5：取消移库     -4：卡车运输或UPSInternational     -3：需要转仓的订单     -2：取消订单     -1：系统自动处理的订单     0：未处理     1：待拣货     2：准备移库     3：当前仓库无库存     4：卡车运输     5：捡货异常     6：锁定库位（暂时没用）     7：待移库（已生成移库指令）     8：已捡货     9：普通订单暂停发货  |
| 8 | timeget  |  |  | date | 3 | 0 | √ |  |  时间  |
| 9 | Checked  |  |  | int | 4 | 0 | √ |  |  捡货顺序  |
| 10 | Pages  |  |  | nvarchar(100) | 200 | 0 | √ |  |     |
| 11 | Shop  |  |  | nvarchar(100) | 200 | 0 | √ |  |  店铺  |
| 12 | Way  |  |  | nvarchar(100) | 200 | 0 | √ |  |  物流  |
| 13 | itemno  |  |  | nvarchar(100) | 200 | 0 | √ |  |     |
| 14 | cangku  |  |  | nvarchar(100) | 200 | 0 | √ |  |  仓库名  |
| 15 | newdateis  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 16 | ACK  |  |  | nvarchar(100) | 200 | 0 | √ |  |     |
| 17 | cpmx  |  |  | nvarchar(500) | 1000 | 0 | √ |  |     |
| 18 | xszje  |  |  | float | 8 | 0 | √ |  |     |




## scb_pickinglist_uk
>    拣货单-英国
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 16 | 0 |  |  |  主键  |
| 2 | NID  |  |  | int | 4 | 0 | √ |  |  订单储值号  |
| 3 | Cpbh  |  |  | nvarchar(50) | 100 | 0 | √ |  |  产品编号  |
| 4 | SKU  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 5 | QTY  |  |  | int | 4 | 0 | √ |  |  拣货数量  |
| 6 | Location  |  |  | varchar(1000) | 1000 | 0 | √ |  |  库位  |
| 7 | SN  |  |  | int | 4 | 0 | √ |  |  状态     -6：取消捡货     -5：取消移库     -4：卡车运输或UPSInternational     -3：需要转仓的订单     -2：取消订单     -1：系统自动处理的订单     0：未处理     1：待拣货     2：准备移库     3：当前仓库无库存     4：卡车运输     5：捡货异常     6：锁定库位（暂时没用）     7：待移库（已生成移库指令）     8：已捡货     9：普通订单暂停发货  |
| 8 | timeget  |  |  | date | 3 | 0 | √ |  |  时间  |
| 9 | Checked  |  |  | int | 4 | 0 | √ |  |  捡货顺序  |
| 10 | Pages  |  |  | nvarchar(100) | 200 | 0 | √ |  |     |
| 11 | Shop  |  |  | nvarchar(100) | 200 | 0 | √ |  |  店铺  |
| 12 | Way  |  |  | nvarchar(100) | 200 | 0 | √ |  |  物流  |
| 13 | itemno  |  |  | nvarchar(100) | 200 | 0 | √ |  |     |
| 14 | cangku  |  |  | nvarchar(100) | 200 | 0 | √ |  |  仓库名  |
| 15 | newdateis  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 16 | sheetNumber  |  |  | int | 4 | 0 | √ |  |     |




## vscb_pickinglist_state
>    拣货单主表对应子表-美国
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 16 | 0 |  |  |  主键  |
| 2 | NID  |  |  | int | 4 | 0 | √ |  |  Xsjl表主键  |
| 3 | TRACKINGNUMBER  |  |  | varchar(100) | 100 | 0 | √ |  |  跟踪号  |
| 4 | SHIPTONAME  |  |  | varchar(100) | 100 | 0 | √ |  |  客户名  |
| 5 | SHIPTOSTREET  |  |  | varchar(100) | 100 | 0 | √ |  |  收货街道  |
| 6 | SHIPTOCITY  |  |  | varchar(100) | 100 | 0 | √ |  |  收货城市  |
| 7 | SHIPTOZIP  |  |  | varchar(100) | 100 | 0 | √ |  |  邮编  |
| 8 | SHIPTOSTATE  |  |  | varchar(100) | 100 | 0 | √ |  |  州  |
| 9 | MULTIITEM  |  |  | int | 4 | 0 | √ |  |     |
| 10 | STATE  |  |  | int | 4 | 0 | √ |  |  状态  |
| 11 | AllGoodsDetail  |  |  | varchar(100) | 100 | 0 | √ |  |  商品详情  |
| 12 | Times  |  |  | date | 3 | 0 | √ |  |  日期  |
| 13 | ACK  |  |  | varchar(100) | 100 | 0 | √ |  |     |
| 14 | Pages  |  |  | int | 4 | 0 | √ |  |     |
| 15 | Way  |  |  | varchar(100) | 100 | 0 | √ |  |  物流  |
| 16 | Store  |  |  | varchar(100) | 100 | 0 | √ |  |     |
| 17 | scaner  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 18 | skip  |  |  | int | 4 | 0 | √ |  |     |
| 19 | temp1  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 20 | temp2  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 21 | temp3  |  |  | varchar(50) | 50 | 0 | √ |  |     |




## vscb_pickinglist_state_gb
>    拣货单主表对应子表-欧盟
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 16 | 0 |  |  |  主键  |
| 2 | NID  |  |  | int | 4 | 0 | √ |  |  Xsjl表主键  |
| 3 | TRACKINGNUMBER  |  |  | varchar(100) | 100 | 0 | √ |  |  跟踪号  |
| 4 | SHIPTONAME  |  |  | varchar(100) | 100 | 0 | √ |  |  客户名  |
| 5 | SHIPTOSTREET  |  |  | varchar(100) | 100 | 0 | √ |  |  收货街道  |
| 6 | SHIPTOCITY  |  |  | varchar(100) | 100 | 0 | √ |  |  收货城市  |
| 7 | SHIPTOZIP  |  |  | varchar(100) | 100 | 0 | √ |  |  邮编  |
| 8 | SHIPTOSTATE  |  |  | varchar(100) | 100 | 0 | √ |  |  州  |
| 9 | MULTIITEM  |  |  | int | 4 | 0 | √ |  |     |
| 10 | STATE  |  |  | int | 4 | 0 | √ |  |  状态  |
| 11 | AllGoodsDetail  |  |  | varchar(100) | 100 | 0 | √ |  |  商品详情  |
| 12 | Times  |  |  | date | 3 | 0 | √ |  |  日期  |
| 13 | ACK  |  |  | varchar(100) | 100 | 0 | √ |  |     |
| 14 | Pages  |  |  | int | 4 | 0 | √ |  |     |
| 15 | Way  |  |  | varchar(100) | 100 | 0 | √ |  |  物流  |
| 16 | Store  |  |  | varchar(100) | 100 | 0 | √ |  |     |
| 17 | scaner  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 18 | skip  |  |  | int | 4 | 0 | √ |  |     |
| 19 | temp1  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 20 | temp2  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 21 | temp3  |  |  | varchar(50) | 50 | 0 | √ |  |     |




## scb_pickinglist_state_uk
>    拣货单主表对应子表-英国
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 16 | 0 |  |  |  主键  |
| 2 | NID  |  |  | int | 4 | 0 | √ |  |  Xsjl表主键  |
| 3 | TRACKINGNUMBER  |  |  | varchar(100) | 100 | 0 | √ |  |  跟踪号  |
| 4 | SHIPTONAME  |  |  | varchar(100) | 100 | 0 | √ |  |  客户名  |
| 5 | SHIPTOSTREET  |  |  | varchar(100) | 100 | 0 | √ |  |  收货街道  |
| 6 | SHIPTOCITY  |  |  | varchar(100) | 100 | 0 | √ |  |  收货城市  |
| 7 | SHIPTOZIP  |  |  | varchar(100) | 100 | 0 | √ |  |  邮编  |
| 8 | SHIPTOSTATE  |  |  | varchar(100) | 100 | 0 | √ |  |  州  |
| 9 | MULTIITEM  |  |  | int | 4 | 0 | √ |  |     |
| 10 | STATE  |  |  | int | 4 | 0 | √ |  |  状态  |
| 11 | AllGoodsDetail  |  |  | varchar(100) | 100 | 0 | √ |  |  商品详情  |
| 12 | Times  |  |  | date | 3 | 0 | √ |  |  日期  |
| 13 | ACK  |  |  | varchar(100) | 100 | 0 | √ |  |     |
| 14 | Pages  |  |  | int | 4 | 0 | √ |  |     |
| 15 | Way  |  |  | varchar(100) | 100 | 0 | √ |  |  物流  |
| 16 | Store  |  |  | varchar(100) | 100 | 0 | √ |  |     |
| 17 | scaner  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 18 | skip  |  |  | int | 4 | 0 | √ |  |     |
| 19 | temp1  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 20 | temp2  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 21 | temp3  |  |  | varchar(50) | 50 | 0 | √ |  |     |




## smz_order_history
>    原始主表数据-美国
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 |  |  | √ | uniqueidentifier | 16 | 0 |  |  |  主键  |




## smz_order_historysheets
>    原始子表数据-美国
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | Number |  | √ | uniqueidentifier | 16 | 0 |  |  |  主键  |
| 2 | Father  |  |  | nvarchar | 100 | 0 | √ |  |  主表Number  |




## Transportation
>  自动处理筛选表  
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | Number |  | √ | uniqueidentifier | 16 | 0 |  |  |  主键  |




## scb_wms_dhldata
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | DeliverTrackNo  |  |  | nvarchar(100) | 200 | 0 |  |  |     |
| 3 | ReturnTrackNo  |  |  | nvarchar(100) | 200 | 0 |  |  |     |
| 4 | Created  |  |  | datetime | 8 | 0 |  |  |     |
| 5 | _MASK_FROM_V2  |  |  | timestamp | 8 | 0 |  |  |     |




## scb_wms_brtdata
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | TrackNo |  |  | varchar(100) | 100 | 0 | √ |  |     |
| 3 | Data  |  |  | varchar(2000) | 2000 | 0 | √ |  |     |
| 4 | Created  |  |  | datetime | 8 | 0 | √ |  |     |




## scb_amazon_ShipmentId
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | ShipmentId  |  |  | varchar(100) | 100 | 0 | √ |  |     |
| 3 | TrackNo  |  |  | varchar(100) | 100 | 0 | √ |  |     |
| 4 | Created  |  |  | datetime | 8 | 0 | √ |  |     |
| 5 | ShipmentStatus  |  |  | varchar(100) | 100 | 0 | √ |  |     |
| 6 | OrderID  |  |  | varchar(100) | 100 | 0 | √ |  |     |




## scb_amazon_postcode
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | decimal(18,0) | 9 | 0 |  |  |  主键  |
| 2 | postcode  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 3 | pingtai  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 4 | country  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 5 | section  |  |  | int | 4 | 0 | √ |  |     |
| 6 | start  |  |  | int | 4 | 0 | √ |  |     |
| 7 | [end]  |  |  | int | 4 | 0 | √ |  |     |




## scb_feiyan_postcode
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | decimal(18,0) | 9 | 0 |  |  |  主键  |
| 2 | postcode  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 3 | pingtai  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 4 | country  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 5 | section  |  |  | int | 4 | 0 | √ |  |     |
| 6 | start  |  |  | int | 4 | 0 | √ |  |     |
| 7 | [end]  |  |  | int | 4 | 0 | √ |  |     |
| 8 | status  |  |  | int | 100 | 0 | √ |  |     |
| 9 | logistics  |  |  | int | 100 | 0 | √ |  |     |
| 10 | describe  |  |  | nvarchar(100) | 100 | 0 | √ |  |     |




## scb_islands_zip
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | zip  |  |  | varchar(50) | 50 | 0 |  |  |     |
| 3 | islands  |  |  | varchar(50) | 50 | 0 | √ |  |     |




## scb_LogisticsForProductAdd
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | numeric(18,0) | 9 | 0 |  |  |  主键  |
| 2 | goods  |  |  | nvarchar(50) | 100 | 0 |  |  |     |
| 3 | fee  |  | float | 8 | 0 |  |  |     |
| 4 | country  |  |  | nvarchar(10) | 20 | 0 |  |  |     |
| 5 | wlfs  |  |  | nvarchar(30) | 60 | 0 | √ |  |     |




## ssmz_invoice_eu
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | orderID  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 3 | orderDate  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 4 | state  |  |  | datetime | 8 | 0 | √ |  |     |
| 5 | store  |  |  | int | 4 | 0 | √ |  |     |
| 6 | Msg  |  |  | nvarchar(2000) | 4000 | 0 | √ |  |     |




## N_goodsSheet
>    商品子表
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | ItemNo1  |  |  | varchar(100) | 100 | 0 | √ |  |     |
| 3 | ItemNo2  |  |  | varchar(100) | 100 | 0 | √ |  |     |
| 4 | country  |  |  | varchar(10) | 10 | 0 | √ |  |     |
| 5 | sort  |  |  | int | 4 | 0 | √ |  |     |
| 6 | TNTDAYS  |  |  | int | 4 | 0 |  |  |     |
| 7 | _MASK_FROM_V2  |  |  | timestamp | 8 | 0 | √ |  |     |
| 8 | addtime  |  |  | datetime | 8 | 0 | √ |  |     |
| 9 | IsFBA  |  |  | int | 4 | 0 |  |  |     |





## scb_WMS_TwoChangeOne
>    订单合并
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | uniqueidentifier | 16 | 0 |  |  |  主键  |
| 2 | ItemNoTwo  |  |  | nvarchar(50) | 100 | 0 |  |  |     |
| 3 | Qty  |  |  | int | 4 | 0 |  |  |     |
| 4 | ItemNoOne  |  |  | nvarchar(50) | 100 | 0 |  |  |     |
| 5 | Creator  |  |  | nvarchar(50) | 100 | 0 |  |  |     |
| 6 | Created  |  |  | datetime | 8 | 0 |  |  |     |




## N_goodsExtend
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | ItemNo  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 3 | country  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 4 | DeclaredValue  |  |  | decimal(18,2) | 9 | 2 | √ |  |     |




## scb_replace_hh
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | decimal(18,0) | 9 | 0 |  |  |  主键  |
| 2 | yhhh  |  |  | nvarchar(255) | 510 | 0 |  |  |     |
| 3 | pingtai  |  |  | nvarchar(255) | 510 | 0 | √ |  |     |
| 4 | dainpu  |  |  | nvarchar(255) | 510 | 0 | √ |  |     |
| 5 | thhh  |  |  | nvarchar(255) | 510 | 0 | √ |  |     |
| 6 | country  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 7 | _MASK_FROM_V2  |  |  | timestamp | 8 | 0 | √ |  |     |




## scb_login
>  用户表  
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | id |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | username  |  |  | nvarchar(50) | 100 | 0 | √ |  |  用户名  |
| 3 | password  |  |  | nvarchar(50) | 100 | 0 | √ |  |  密码  |
| 4 | departmentid  |  |  | int | 4 | 0 | √ |  |  部门ID  |
| 5 | department  |  |  | nvarchar(50) | 100 | 0 | √ |  |  部门  |
| 6 | realname  |  |  | nvarchar(50) | 100 | 0 | √ |  |  本名  |
| 7 | roleid  |  |  | nvarchar(50) | 100 | 0 | √ |  |  职务id  |
| 8 | rolename  |  |  | nvarchar(50) | 100 | 0 | √ |  |  职务  |
| 9 | ischeck  |  |  | int | 4 | 0 | √ |  |     |
| 10 | isadministrator  |  |  | int | 4 | 0 | √ |  |  是否管理员  |
| 11 | temp  |  |  | int | 4 | 0 | √ |  |  临时标记  |
| 12 | quota  |  |  | decimal(18,2) | 9 | 0 |  |  |  备注  |
| 13 | pm_ischeck  |  |  | int | 4 | 0 |  |  |     |
| 14 | pm_isadmin  |  |  | int | 4 | 0 |  |  |     |
| 15 | pm_role  |  |  | nvarchar(50) | 100 | 0 |  |  |     |
| 16 | pm_country  |  |  | nvarchar(500) | 1000 | 0 |  |  |     |
| 17 | pm_menu  |  |  | nvarchar(500) | 1000 | 0 | √ |  |     |
| 18 | country  |  |  | nchar(10) | 20 | 0 | √ |  |  国家  |
| 19 | outFlag  |  |  | nchar(500) | 1200 | 0 | √ |  |     |
| 20 | isManager  |  |  | nchar(1) | 2 | 0 | √ |  |  是否管理  |
| 21 | pm_wenan  |  |  | int | 4 | 0 |  |  |     |
| 22 | ckbelong  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 23 | isForeign  |  |  | bit | 1 | 0 | √ |  |     |
| 24 | isAccept  |  |  | bit | 1 | 0 |  |  |     |
| 25 | PM_FUID  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 26 | pm_gradeid  |  |  | varchar(200) | 200 | 0 | √ |  |     |
| 27 | old_pm_menu  |  |  | nvarchar(500) | 1000 | 0 | √ |  |     |
| 28 | pm_instructions_role  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 29 | pm_isAmazonyy  |  |  | bit | 1 | 0 | √ |  |     |
| 30 | pm_StoreAuthFlag  |  |  | bit | 1 | 0 | √ |  |     |





## scb_third_party
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | Logistics  |  |  | nvarchar(50) | 100 | 0 |  |  |     |
| 3 | Shop  |  |  | nvarchar(50) | 100 | 0 |  |  |     |
| 4 | AccountNumber  |  |  | nvarchar(50) | 100 | 0 |  |  |     |
| 5 | CountryCode  |  |  | nvarchar(10) | 20 | 0 | √ |  |     |
| 6 | PostalCode  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 7 | WarehouseId  |  |  | nvarchar(50) | 8 | 0 | √ |  |     |
| 8 | _MASK_FROM_V2  |  |  | timestamp | 8 | 0 |  |  |     |





## scb_WMS_MatchRule
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | Country  |  |  | nvarchar(10) | 20 | 0 | √ |  | 指定规则适用的国家 |
| 3 | User  |  |  | nvarchar(20) | 40 | 0 | √ |  | 规则用途标识 |
| 4 | Type  |  |  | nvarchar(100) | 200 | 0 | √ |  | 规则类型 |
| 5 | Warehouse  |  |  | nvarchar(200) | 400 | 0 | √ |  | 仓库范围 |
| 6 | Logistics  |  |  | nvarchar(200) | 400 | 0 | √ |  | 物流方式 |
| 7 | ItemNo  |  |  | nvarchar(4000) | 8000 | 0 | √ |  | 商品编号范围 |
| 8 | Shop  |  |  | nvarchar(4000) | 8000 | √ |  |     |店铺范围|
| 9 | Province  |  |  | nvarchar(4000) | 8000 | 0 | √ |  | 省份/州范围 |
| 10 | State  |  |  | int | 4 | 0 |  |  | 规则状态 |
| 11 | _MASK_FROM_V2  |  |  | timestamp | 8 | 0 |  |  |     |





## scb_WMS_MatchRule_Deatils
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | RID  |  |  | int | 4 | 0 |  |  |     |
| 3 | Typr  |  |  | varchar(50) | 50 | 0 |  |  |     |
| 4 | Parameter  |  |  | varchar(100) | 100 | 0 |  |  |     |
| 5 | _MASK_FROM_V2  |  |  | timestamp | 8 | 0 | √ |  |     |






## scb_WMS_transship
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ShipmentID |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | SerialNumber  |  |  | varchar(100) | 100 | 0 | √ |  |     |
| 3 | FromWarehouse  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 4 | ToWarehouse  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 5 | DockNo  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 6 | State  |  |  | int | 4 | 0 | √ |  |     |
| 7 | Creator  |  |  | varchar(100) | 100 | 0 | √ |  |     |
| 8 | Created  |  |  | datetime | 8 | 0 | √ |  |     |
| 9 | FinishedTime  |  |  | datetime | 8 | 0 | √ |  |     |
| 10 | StartTime  |  |  | datetime | 8 | 0 | √ |  |     |
| 11 | isNewCreate  |  |  | int | 4 | 0 | √ |  |     |
| 12 | IsDone  |  |  | int | 4 | 0 | √ |  |     |
| 13 | _MASK_FROM_V2  |  |  | binary(8) | 8 | 0 | √ |  |     |
| 14 | Freight  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |






## wms_Order_Supplementary
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | Number |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | RelID  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 3 | RelType  |  |  | varchar(50) | 0 0 | √ |  |     |
| 4 | State  |  |  | int | 4 | 0 | √ |  |     |
| 5 | logicswayoid  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 6 | warehouseid  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 7 | Message  |  |  | varchar(200) | 200 | 0 | √ |  |     |
| 8 | Created  |  |  | datetime | 8 | 0 | √ |  |     |






## scb_WMS_Third_Order
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | NID  |  |  | decimal(18,0) | 9 | 0 |  |  |     |
| 3 | filterflag  |  |  | int | 4 | 0 |  |  |     |
| 4 | state  |  |  | int | 4 | 0 |  |  |     |
| 5 | IsComplete  |  |  | int | 4 | 0 |  |  |     |
| 6 | Created  |  |  | datetime | 8 | 0 | √ |  |     |
| 7 | ErrorMessage  |  |  | varchar(100) | 100 | 0 | √ |  |     |






## scb_xsjl_helper
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | Number  |  |  | decimal(18,0) | 9 | 0 |  |  |     |
| 3 | Pallets  |  |  | int | 4 | 0 |  |  |     |






## scb_WMS_File
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | RelID  |  |  | varchar(100) | 100 | 0 | √ |  |     |
| 3 | RelType  |  |  | varchar(100) | 100 | 0 | √ |  |     |
| 4 | FileName  |  |  | varchar(200) | 200 | 0 | √ |  |     |
| 5 | FileUrl  |  |  | varchar(500) | 500 | 0 | √ |  |     |
| 6 | State  |  |  | int | 4 | 0 |  |  |     |






## scb_xsjlsheet_income
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | number |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | father  |  |  | int | 4 | 0 | √ |  |     |
| 3 | income  |  |  | decimal(18,4) | 9 | 4 | √ |  |     |
| 4 | created  |  |  | datetime | 8 | 0 | √ |  |     |
| 5 | filename  |  |  | varchar(300) | 300 | 0 | √ |  |     |






## scb_UsedOrder
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | Id |  | √ | uniqueidentifier | 16 | 0 |  |  |  主键  |
| 2 | NID  |  |  | decimal(18,0) | 9 | 0 | √ |  |     |
| 3 | Orderid  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 4 | OrderState  |  |  | int | 4 | 0 | √ |  |     |
| 5 | Temp1  |  |  | varchar(50) | 50 | 0 | √ |  |     |






## scb_WMS_Zpl
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | uniqueidentifier | 16 | 0 |  |  |  主键  |
| 2 | NID  |  |  | decimal(18,0) | 9 | 0 |  |  |     |
| 3 | TrackNo  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 4 | ZplBarCode  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 5 | State  |  |  | int | 4 | 0 |  |  |     |
| 6 | Created  |  |  | datetime | 8 | 0 |  |  |     |






## scb_fz_stock
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | id |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | father  |  |  | int | 4 | 0 | √ |  |     |
| 3 | groupid  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 4 | cpbh  |  |  | varchar(50) | 50 | 0 | √ |  |  产品编号  |
| 5 | kcxs  |  |  | int | 4 | 0 | √ |  |  库存箱数   |
| 6 | chxs  |  |  | int | 4 | 0 | √ |  |  出货箱数  |
| 7 | jhrq  |  |  | varchar(50) | 50 | 0 | √ |  |  交货日期  |
| 8 | bhxs  |  |  | int | 4 | 0 | √ |  |   补货箱数  |
| 9 | thxs  |  |  | int | 4 | 0 | √ |  |   退货箱数  |
| 10 | basekc  |  |  | int | 4 | 0 | √ |  |  基础库存   |
| 11 | zrkc  |  |  | int | 4 | 0 | √ |  |  转入库存  |
| 12 | zckc  |  |  | int | 4 | 0 | √ |  |  转出库存   |






## scb_fz_message
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | number |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | fromname  |  |  | varchar(100) | 100 | 0 |  |  |     |
| 3 | toname  |  |  | varchar(100) | 100 | 0 |  |  |     |
| 4 | temp  |  |  | varchar(500) | 500 | 0 | √ |  |     |
| 5 | modedate  |  |  | datetime | 8| 0 | √ |  |     |
| 6 | status  |  |  | int | 4 | 0 |  |  |  状态       |
| 7 | AllotType  |  |  | varchar(20) | 20 | 0 | √ |  |     |





## scb_fz_message_dtl
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | number |  | √ | int | 16 | 0 |  |  |  主键  |
| 2 | father  |  |  | int | 100 | 0 |  |  |  父节点  |
| 3 | cpbh  |  |  | varchar(50) | 50 | 0 |  |  |  产品编号  |
| 4 | cbdj  |  |  | float | 8 | 0 |  |  |  成本单价  |
| 5 | kcsl  |  |  | int | 4 | 0 |  |  |  库存数量  |
| 6 | sqsl  |  |  | int | 4 | 0 |  |  |  申请数量  |
| 7 | shsl  |  |  | int | 4 | 0 |  |  |     |
| 8 | shr  |  |  | varchar(50) | 50 | 0 |  |  |     |
| 9 | shzt  |  |  | int | 4 | 0 |  |  |     |
| 10 | zssl  |  |  | int | 4 | 0 |  |  |     |
| 11 | zsr  |  |  | varchar(50) | 50 | 0 |  |  |  总收入   |
| 12 | zszt  |  |  | int | 4 | 0 |  |  |     |
| 13 | zje  |  |  | float | 8 | 0 |  |  |  总金额  |
| 14 | temp  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 15 | groupid  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 16 | sgroupid  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 17 | AllocationDate  |  |  | datetime | 8 | 0 | √ |  |     |
| 18 | AllotType  |  |  | varchar(20) | 20 | 0 | √ |  |     |






## scb_kcjl_bill
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | number |  | √ | decimal(18,0) | 9 | 0 |  |  |  主键  |
| 2 | cpbh  |  |  | nvarchar(50) | 100 | 0 | √ |  |  产品编号  |
| 3 | sl  |  |  | int | 4 | 0 | √ |  |  数量  |
| 4 | Location  |  |  | nvarchar(50) | 100 | 0 | √ |  |  坐标  |
| 5 | Warehouse  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 6 | BillofLodingNo  |  |  | nvarchar(500) | 1000 | 0 | √ |  |  订单下载序号  |
| 7 | ImportTime  |  |  | datetime | 8 | 0 | √ |  |  输入时间  |
| 8 | operator  |  |  | nvarchar(50) | 100 | 0 | √ |  |  操作人  |
| 9 | isFCL  |  |  | nvarchar(20) | 40 | 0 | √ |  |  是否FCL  |
| 10 | boxno  |  |  | nvarchar(50) | 100 | 0 | √ |  |  箱 编号  |






## wms_temp_location
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | ItemNumber  |  |  | nvarchar(100) | 200 | 0 | √ |  |     |
| 3 | Warehouse  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 4 | TimeGet  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 5 | Location  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 6 | QTY  |  |  | int | 4 | 0 | √ |  |     |






## wms_temp_kcjl
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | ID |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | ItemNumber  |  |  | nvarchar(100) | 200 | 0 | √ |  |     |
| 3 | Warehouse  |  |  | nvarchar(50) | 100 | 0 | √ |  |     |
| 4 | PrepccupiedQuantity  |  |  | int | 4 | 0 | √ |  |     |
| 5 | QuantityCancelled  |  |  | int | 4 | 0 | √ |  |     |
| 6 | ConsumedQuantity  |  |  | int | 4 | 0 | √ |  |     |






## scb_tempkcsl
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | number |  | √ | numeric(18,0) | 4 | 0 |  |  |  主键  |
| 2 | cpbh  |  |  | varchar(50) | 50 | 0 | √ |  |  产品编号  |
| 3 | ck  |  |  | varchar(50) | 50 | 0 | √ |  |   仓库  |
| 4 | nowdate  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 5 | sl  |  |  | int | 4 | 0 | √ |  |  数量  |
| 6 | xzsl  |  |  | int | 4 | 0 | √ |  |  新增数量   |
| 7 | kind  |  |  | int | 4 | 0 | √ |  |  种类   |






## Scb_Logisticsmode
>    
| 字段序号 | 字段名 | 标识 | 主键 | 类型 | 占用字节数 | 小数位数 | 允许空 | 默认值 | 字段说明 |
| ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ | ------------ |
| 1 | oid |  | √ | int | 4 | 0 |  |  |  主键  |
| 2 | operatoren  |  |  | nvarchar(50) | 100 | 0 | √ |  |  操作人（英文）  |
| 3 | operatorcn  |  |  | nvarchar(50) | 100 | 0 | √ |  |  操作人（中文）  |
| 4 | logisticsmode  |  |  | nvarchar(50) | 100 | 0 | √ |  | 物流方式名称 |
| 5 | specificlogistics  |  |  | nvarchar(50) | 100 | 0 | √ |  | 具体物流信息 |
| 6 | autotrackingno  |  |  | int | 4 | 0 | √ |  | 自动跟踪号标识 |
| 7 | servicecode  |  |  | nvarchar(50) | 100 | 0 | √ |  | 物流提供商的服务代码 |
| 8 | automatchorder  |  |  | int | 4 | 0 | √ |  | 是否自动匹配订单（0/1） |
| 9 | sorting  |  |  | int | 4 | 0 | √ |  | 排序顺序 |
| 10 | logisticscompany  |  |  | nvarchar(150) | 300 | 0 | √ |  | 物流公司名称 |
| 11 | remark  |  |  | nvarchar(1000) | 2000 | 0 | √ |  | 备注信息 |
| 12 | checkeds  |  |  | int | 4 | 0 | √ |  | 检查状态 |
| 13 | warehoues  |  |  | nvarchar(500) | 1000 | 0 | √ |  | 适用仓库 |
| 14 | IsEnable  |  |  | int | 4 | 0 | √ |  | 启用状态 |
| 15 | Label_Suffix  |  |  | nvarchar(20) | 40 | 0 | √ |  |     |
| 16 | Countries  |  |  | nvarchar(20) | 40 | 0 | √ |  |  国家  |
| 17 | logisticsName  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 18 | Incoice_Suffix  |  |  | varchar(50) | 50 | 0 | √ |  |     |
| 19 | _MASK_FROM_V2  |  |  | timestamp | 16 | 0 | √ |  |     |