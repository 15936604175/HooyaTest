using Newtonsoft.Json;
using ShopShipperLogistics.Upload.Ebay;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Http;
using wms_api.App_Start;
using wms_api.Models;
using wms_api.Models.Logistics;
using wms_api.Models.Match;
using wms_api.Models.Wms;
using wms_api.Other;
using WMSClassLibrary;
using WMSClassLibrary.DbContexts;
using WMSClassLibrary.Entity;
using WMSClassLibrary.Entity.Logistics;
using WMSClassLibrary.Helper;
using WMSClassLibrary.Model;
using static wms_api.Models.LTLQuoteExtRequest;

namespace wms_api.Controllers
{
	[RoutePrefix("V1/Match")]
	[ActionFilter]
	public class MatchController : ApiController
	{
		private List<scb_ups_zip> _cacheZip;
		private List<scb_ontrac_zip> _cacheOntracZip;
		private List<scb_gofo_zip> _cacheGOFOZip;
		private List<scb_fimile_zip> _cacheFimileZip;
		private List<scb_western_post_zip> _cacheWesternPostZip;
		private List<scb_xmile_zip> _cacheXmileZip;
		private List<scb_roadie_zip> _cacheRoadieZip;
		//private static List<string> _UPSSkuList;
		private static List<scb_third_party> _ThirdBillAccountList;
		#region 对外

		[Route("GetPreMatchWare")]
		public int GetPreMatchWare(string country, string cpbh, int count, string zip, string tocountry)
		{
			var warehouseid = 0;
			var db = new cxtradeModel();
			var scbcks = db.scbck.ToList();

			if (tocountry != "US")
			{
				return tocountry == "CA" ? 141 : 0;
			}

			//获取有效库存
			var tempKcslHelper = new TempKcslHelper();
			var stockList = tempKcslHelper.GetStockInfoByCpbhs(new List<string>() { cpbh }, country, DateTime.Now.ToString("yyyy-MM-dd"), count);
			if (tocountry == "CA")
			{
				stockList = stockList.Where(p => p.WarehouseNo == "US21").ToList();
			}
			else if (tocountry == "US")
			{
				stockList = stockList.Where(p => p.WarehouseNo != "US21" && p.WarehouseNo.StartsWith("US")).ToList();
			}

			////获取已经预占用的库存
			//var prestocks = db.

			//优先获取有库存的
			if (stockList.Any())
			{
				var warehouseNos = stockList.Select(p => p.WarehouseNo).ToList();
				var cks = scbcks.Where(p => warehouseNos.Contains(p.id)).OrderBy(p => p.AreaSort).ToList();
				var zone = 0;
				warehouseid = cks.First().number;
				foreach (var ck in cks)
				{
					var itemzone = _cacheZip.Where(p => zip.StartsWith(p.DestZip) && p.Origin == ck.Origin).Select(p => p.GNDZone).FirstOrDefault();
					if (itemzone != null)
					{
						if (zone > 0 && zone < itemzone)
						{

						}
						else
						{
							zone = itemzone.Value;
							warehouseid = ck.number;
						}
					}
				}
			}
			else
			{
				//获取未卸柜库存数量
				var sql = $"select cpbh,place WarehouseNo,(isnull(transfer200sl,0) + isnull(cabinet_unloaded,0)) EffectiveInventory from scb_kcjl_area_rpt where (isnull(transfer200sl,0) + isnull(cabinet_unloaded,0))-{count} >0 and cpbh ='{cpbh}'";
				var unloadStocks = db.Database.SqlQuery<StockInfoModel>(sql).ToList();
				if (tocountry == "CA")
				{
					unloadStocks = unloadStocks.Where(p => p.WarehouseNo == "US21").ToList();
				}
				else if (tocountry == "US")
				{
					unloadStocks = unloadStocks.Where(p => p.WarehouseNo != "US21" && p.WarehouseNo.StartsWith("US")).ToList();
				}

				//没有库存，考虑未卸柜的
				if (unloadStocks.Any())
				{
					var warehouseNos = unloadStocks.Select(p => p.WarehouseNo).ToList();
					var cks = scbcks.Where(p => warehouseNos.Contains(p.id)).OrderBy(p => p.AreaSort).ToList();
					var zone = 0;
					warehouseid = cks.First().number;
					foreach (var ck in cks)
					{
						var itemzone = _cacheZip.Where(p => zip.StartsWith(p.DestZip) && p.Origin == ck.Origin).Select(p => p.GNDZone).FirstOrDefault();
						if (itemzone != null)
						{
							if (zone > 0 && zone < itemzone)
							{

							}
							else
							{
								zone = itemzone.Value;
								warehouseid = ck.number;
							}
						}
					}
				}
				//都没有，就默认US16
				else
				{
					if (country == "US")
					{
						warehouseid = scbcks.Where(p => p.id == "US16").Select(p => p.number).First();
					}
				}
			}
			return warehouseid;
		}


		/// <summary>
		/// US
		/// </summary>
		/// <param name="number">订单主键</param>
		/// <returns></returns>
		[Route("Order")]
		public OrderMatchResult Order(decimal number)
		{
			return BaseOrder(number, "");
		}

		[Route("OrderByDate")]
		public OrderMatchResult OrderByDate(decimal number, string Date)
		{
			return BaseOrder(number, Date);
		}

		/// <summary>
		/// EU
		/// </summary>
		/// <param name="number">订单主键</param>
		/// <returns></returns>
		[Route("EU/Order"), Route("DE/Order")]
		public OrderMatchResult OrderByEU(decimal number)
		{
			try
			{
				var db = new cxtradeModel();
				var xsjl = db.scb_xsjl.FirstOrDefault(p => p.number == number);
				if (xsjl != null)
				{
					var model = MapperHelper.Mapper<EUMatchModel, scb_xsjl>(xsjl);
					model.number = number;
					//model.isReturn = xsjl.temp3 == "7";
					//model.isdev = true;
					//model.toCountry = xsjl.temp10;
					var sheets = db.scb_xsjlsheet.Where(p => p.father == number).ToList();
					foreach (var sheet in sheets)
					{
						var detail = MapperHelper.Mapper<EUMatchSheetModel, scb_xsjlsheet>(sheet);
						//detail.price = (decimal)sheet.wxdj;
						model.sheets.Add(detail);
					}
					return BaseOrderByEUV3(model);
				}
				else
				{
					return new OrderMatchResult()
					{
						SericeType = string.Empty,
						Result = new ResponseResult() { State = 0, Message = "订单不存在或者状态已变更，请重新查询！" }
					};
				}
				//return BaseOrderByEU(number);
			}
			catch (Exception ex)
			{
				//OrderMatchResult.Result.Message = ex.Message;
				Helper.Error(number, "wms_api", ex);
				return new OrderMatchResult()
				{
					SericeType = string.Empty,
					Result = new ResponseResult() { State = 0, Message = ex.Message }
				};
			}
		}
		/// <summary>
		/// EU
		/// </summary>
		/// <param name="number">订单主键</param>
		/// <returns></returns>
		[Route("EU/OrderV3"), Route("DE/OrderV3")]
		public OrderMatchResult OrderByEUV3([FromBody] EUMatchModel model)
		{
			return BaseOrderByEUV3(model);
		}

		/// <summary>
		/// GB
		/// </summary>
		/// <param name="number">订单主键</param>
		/// <returns></returns>
		[Route("UK/Order"), Route("GB/Order")]
		public OrderMatchResult OrderByGB(decimal number)
		{
			return BaseOrderByGB(number);
		}

		/// <summary>
		/// AU
		/// </summary>
		/// <param name="number">订单主键</param>
		/// <returns></returns>
		[Route("AU/Order")]
		public OrderMatchResult OrderByAU(decimal number)
		{
			return BaseOrderByAU(number);
		}

		/// <summary>
		/// US
		/// </summary>
		/// <param name="number">订单主键</param>
		/// <returns></returns>
		[Route("US/Order")]
		public OrderMatchResult OrderByUS(decimal number)
		{
			return BaseOrderByUSV2(number, "");
		}

		/// <summary>
		/// US 排除AMZN
		/// </summary>
		/// <param name="number">订单主键</param>
		/// <returns></returns>
		[Route("US/Order")]
		public OrderMatchResult OrderByUSIgnoreAMZN(decimal number, bool ignoreAMZN)
		{
			return BaseOrderByUSV2(number, "", ignoreAMZN);
		}

		/// <summary>
		/// 美国最新匹配
		/// </summary>
		/// <param name="number">订单主键</param>
		/// <returns></returns>
		[Route("US/Order_2")]
		public OrderMatchResult OrderByUSV2(decimal number, string Date)
		{
			return BaseOrderByUSV2(number, Date);
		}
		/// <summary>
		/// 美国最新匹配
		/// </summary>
		/// <param name="number">订单主键</param>
		/// <returns></returns>
		[Route("US/Order_2")]
		public OrderMatchResult OrderByUSV2(decimal number)
		{
			return BaseOrderByUSV2(number, "");
		}
		/// <summary>
		/// 美国最新匹配
		/// </summary>
		/// <param name="number">订单主键</param>
		/// <returns></returns>
		[Route("US/Order_2")]
		public OrderMatchResult OrderByUSV2(decimal number, string Date, bool ignoreAMZN)
		{
			return BaseOrderByUSV2(number, Date, ignoreAMZN);
		}

		/// <summary>
		/// 美国最新匹配
		/// </summary>
		/// <param name="number">订单主键</param>
		/// <returns></returns>
		[Route("US/Order_3")]
		public OrderMatchResult OrderByUSV3([FromBody] MatchModel model)
		{
			return BaseOrderByUSV3(model, "", model.isdev);
		}

		[Route("US/Order_3")]
		public OrderMatchResult OrderByUSV3(decimal number, bool ignoreSize = false)
		{
			var db = new cxtradeModel();
			var xsjl = db.scb_xsjl.FirstOrDefault(p => p.number == number);
			var model = MapperHelper.Mapper<MatchModel, scb_xsjl>(xsjl);
			model.number = number.ToString();
			model.isReturn = xsjl.temp3 == "7";
			model.xszje = (decimal)xsjl.xszje;
			model.isdev = true;
			model.toCountry = xsjl.temp10;
			var sheets = db.scb_xsjlsheet.Where(p => p.father == number).ToList();
			foreach (var sheet in sheets)
			{
				var detail = MapperHelper.Mapper<MatchDetailModel, scb_xsjlsheet>(sheet);
				detail.price = (decimal)sheet.wxdj;
				model.details.Add(detail);
			}
			model.ignoreSize = ignoreSize;

			return BaseOrderByUSV3(model, "", model.isdev);
		}

		/// <summary>
		/// 美国卡车派单查询
		/// </summary>
		/// <param name="number">订单主键</param>
		/// <returns></returns>
		[Route("US/Truck")]
		public ResponseResult OrderByUS_Truck(decimal number)
		{
			return US_Truck(number, "");
		}
		/// <summary>
		/// 澳洲匹配运费
		/// </summary>
		/// <param name="number">订单主键</param>
		/// <returns></returns>
		[Route("AU/MatchFreight")]
		public AuMatchFreightResult MatchFreight(AuMatchFreight model)
		{
			return Freight(model);
		}

		#endregion

		#region 中间



		private Tuple<string, decimal> GetLTLFeeByOS(string country, string xspt, string dianpu,
			string city, string statsa, string zip, string khname, string phone, string adress, string address1,
			string cknumber, string ckid, List<CpbhSizeInfo> cpExpressSizeinfos)
		{
			var message = string.Empty;
			var fee = -1m;

			var db = new cxtradeModel();

			try
			{

				List<Items> items = new List<Items>();
				foreach (var item in cpExpressSizeinfos)
				{
					items.Add(new Items()
					{
						prdtNo = item.cpbh,
						qty = item.qty
					});
				}

				var cpExpressSizeinfo = cpExpressSizeinfos.First();

				//48*40*（货品高+5）    货品重量+35lb,如果长超了48，就用货品的长，向上取整
				var pallets = new List<Pallet>() {
					new Pallet()
					{
						length = cpExpressSizeinfo.bzcd > 48 ? (int)Math.Ceiling(cpExpressSizeinfo.bzcd) : 48,
						width = 40,
						height =(int)Math.Ceiling(cpExpressSizeinfo.bzgd) + 5,
						weight = (int)Math.Ceiling((decimal)cpExpressSizeinfo.weight) + 35,
						items = items
					}
				};

				var ckset = db.scb_setsender.Where(a => a.logisticsoid == cknumber).FirstOrDefault();
				if (ckset == null)
				{
					throw new Exception($"仓库:{ckid} 获取仓库寄件信息失败");
				}
				var origin = new AddressParam()
				{
					city = ckset.city.Replace("|", ""),
					states = ckset.state.Replace("|", ""),
					postcode = !string.IsNullOrEmpty(ckset.zip) ? ckset.zip.Length >= 5 ? ckset.zip.Substring(0, 5) : ckset.zip.PadRight(5, '0').Substring(0, 5) : "00000",
					ContactMan = ckset.names,
					Phone = ckset.phone,
					Address1 = ckset.address
				};
				//收货地
				var detination = new AddressParam()
				{
					city = city,
					states = statsa,
					postcode = zip,
					ContactMan = khname,
					Phone = phone,
					Address1 = adress,
					Address2 = address1
				};

				var request = new LTLQuoteExtRequest()
				{
					country = country,
					warehouse = ckid,
					platform = xspt,
					store = dianpu,
					destinationServices = new string[] { "DestinationLiftgate", "LimitedAccessDelivery" },//DestinationLiftgate目的地升降梯LimitedAccessDelivery住宅区
					origin = origin,
					destination = detination,
					pallets = pallets
				};
				List<Header> header = new List<Header>();
				header.Add(new Header()
				{
					Key = "SYSTOKEN",
					Value = "802460a22bdd4aaabe740b4f3c4c1842"
				});
				var json = JsonConvert.SerializeObject(request);
				var result = HttpHandle.BaseApi("http://192.168.0.116:8090/api/LtlCarrier/GetQuoteExt", json, "POST", "application/json", header);
				var response = JsonConvert.DeserializeObject<LTLQuoteExtResponse>(result);
				message = "result:" + json + "; response:" + result; //记下接口返回结果，写到日志里
				if (response.code == 200)
				{
					var charges = new List<decimal>();
					string pattern = @"\$[^$]*";  // 匹配以 $ 开头，直到下一个 $ 或结尾
					foreach (var data in response.data)
					{
						Match match = Regex.Match(data.charge, pattern);
						charges.Add(decimal.Parse(match.Value.Replace("$", "")));
					}
					if (charges.Count > 0)
					{
						fee = charges.Min(); //.OrderBy(p => p).First();
					}
				}

			}
			catch (Exception ex)
			{
				message = ex.Message;
			}
			return new Tuple<string, decimal>(message, fee);
		}


		/// <summary>
		/// 澳洲匹配运费计算
		/// 20241127 何雨梦 1、匹配运算都是真实尺寸；2、长度向上取整后参与运算；3、尺寸判断，用真实尺寸；
		/// </summary>
		private AuMatchFreightResult Freight(AuMatchFreight model)
		{
			#region 初始化
			var db = new cxtradeModel();
			decimal DFEFeeSum = model.BasicCharge;
			AuMatchFreightResult Result = new AuMatchFreightResult()
			{
				State = false
			};
			FreightResult freightResult = new FreightResult();
			freightResult.FeeSum = 0;
			freightResult.AUMsg = string.Empty;
			//2025-06-30 何雨梦 7.01aupost上涨 1.95%
			decimal AUPostPrice = 1.0195M;
			//aupost 2022年 8月燃油费率8% 9月涨价到10%,2022-09-15,aupost 的燃油费率涨到11.5%
			//2023-08-04 改成1.21
			//20240705 改成1.12
			//20241119 改成1.07
			//20251121 价卡1.08
			decimal AUPostRate = 1.08M;
			//string AUPostRateShow = "1.1";
			decimal AUDFERate = 1.16M;   //20250305 何雨梦 DFE燃油费1.175 调整为 1.16
										 //20241119 何雨梦 DFE燃油费1.2 调整为1.175
										 //20241023 何雨梦 DFE燃油费1.23 调整为1.2
										 //20240228 何雨梦 DFE燃油附加费调整，OS的地址查询工具和eshop的计算公式系数请调整为1.25调整为1.23
										 //20231221 何雨梦 DFE燃油费调整 上涨到24.8%  计算公式系数请调整为1.25
										 // DEF 8月燃油费率18.9% 公式请更新为1.2
			decimal AUDFETotalRate = 1.05M; //何雨梦2022-10-14要求加，整个翻1.1倍
			decimal AUTGETotalRate = 1.05M; //何雨梦2025-07-31要求加
											//何雨梦 20241129 DFE的加价系数从原来的1.1改成1.05
											//蒋建涛 priority的燃油更新到18% IPEC更新到13%
			decimal TGEFLRate_Priority = 1.12M; //燃油附加费
			decimal TGEFLRate_IPEC = 1.12M; //燃油附加费
			decimal TGETier1Fee_IPEC = 10M;
			decimal TGETier2Fee_IPEC = 63.5M;
			decimal TGEOtherFeeSum = 0M;
			decimal TGEVolumeSum = 0M;
			decimal other = 0M;
			decimal TGEOther = 1.25M;//TGE QLD强制附加费

			decimal ExtralongSurcharge = 14M; //11.5M;// 9.5M; //超长附加费
			#endregion
			var BillingWeights_AUDFE_List = new List<string>();
			var Weight_AUDFE_List = new List<double>();
			var Volume_real_AUDFE_List = new List<double>();
			var Other = new List<string>();
			string YfFormula = string.Empty;//公式信息
			double SumVolume = 0;
			double SumWeight = 0;
			var AupostOldCpbh = new List<string>()
			{
  "CM24605AU","FH10118NY","FH10118GR","FH10127PI","FH10128CS","FH10128PI","FH10128RO","FH10141BL","FH10126WH","FH10148PI","FH10141YW","FH10095GN","FH10095RE","FH10099MX","FH10099NY","FH10099PI","FH10126CL","FH10126GN","FH10126OR","FH10126PI","HY10222PI","SP37088","SP37494","FH10148BL","FH10095PU","FH10099CS","FH10099GN","FH10099RO","FH10148RE","FH10148YW","FH10095HS","FH10095MT","FH10095RO","SP37495GN","SP37495RE","FH10128BL","FH10135BL","FH10135CL","SP37495BL","FH10120PI","FH10145BL","FH10095BL","FH10095NY","FH10168RO","FH10168WH","FH10132CS","FH10149WH","HZ10269","FH10120YW","CM25020AU","HV10048BL","HV10048PI","HZ10271","FH10117BL","FH10117PU","HW66278BL","HW66637GN","SP37141FZ","FH10117FS","CM24899AU","CM24771AU","MU10024","HZ10288-M","HZ10289-M","HZ10290-M","HZ10292-M","CM24888AU","CM24891AU","CM24902AU","HW54190PI"
			};
			try
			{
				foreach (var sheet in model.sheets)
				{
					var sizes = new List<double?>();  //尺寸，不含小数
					var good = db.N_goods.Where(g => g.cpbh == sheet.cpbh && g.country == "AU" && g.groupflag == 0).FirstOrDefault();
					var size = db.scb_realsize.Where(g => g.cpbh == sheet.cpbh && g.country == "AU").FirstOrDefault();
					var cp = db.cp.Where(a => a.cpbh == sheet.cpbh && a.special_shaped == true).FirstOrDefault();
					if (cp != null)
					{
						sizes.Add(Convert.ToDouble(Math.Floor(Convert.ToDecimal(good.bzgd))));
						sizes.Add(Convert.ToDouble(Math.Floor(Convert.ToDecimal(good.bzcd))));
						sizes.Add(Convert.ToDouble(Math.Floor(Convert.ToDecimal(good.bzkd))));
					}
					else
					{
						sizes.Add(Convert.ToDouble(Math.Ceiling(Convert.ToDecimal(size.bzgd))));
						sizes.Add(Convert.ToDouble(Math.Ceiling(Convert.ToDecimal(size.bzcd))));
						sizes.Add(Convert.ToDouble(Math.Ceiling(Convert.ToDecimal(size.bzkd))));
					}

					var sizes_point = new List<double?>() { size.bzgd, size.bzcd, size.bzkd }; //尺寸 ，含小数

					var weight_kg_point = Math.Round((double)good.weight / 1000, 2); //毛重，含小数，单位kg
					SumWeight += weight_kg_point;
					Weight_AUDFE_List.Add(weight_kg_point * sheet.sl);
					var Weight = Math.Ceiling((double)good.weight / 1000);//实重   毛重，向上取整，单位kg
					var Volume = (double)(good.bzcd * good.bzgd * good.bzkd / 1000000);//打单尺寸计算体积重
					SumVolume += Volume;
					var Volume_real = (double)sizes.Aggregate((total, next) => total * (next ?? 0)) / 1000000; //(double)(good.bzcd * good.bzgd * good.bzkd / 1000000);//真实尺寸计算体积重
					freightResult.Volume_total += Volume * sheet.sl;
					var Volume_Weight_tmp = Volume_real * 250;
					Volume_real_AUDFE_List.Add(Volume_Weight_tmp * sheet.sl);
					var Volume_Weight = Math.Ceiling(Volume * 250);//体积重 
																   //2022年11月2号调整 AUPost计费重=毛重，不再计算体积重，X=ROUNDUP(毛重,0) 
																   //var BillingWeight = Weight;//计费重 //Weight > Volume_Weight ? Weight : Volume_Weight;//计费重
					var Volume_WeightReal = Volume_real > 0 ? Math.Ceiling(Volume_real * 250) : Volume_Weight;//真实尺寸的体积重 ，给TGE用的

					//计费重
					var BillingWeight = 0d;
					var maxNum = sizes.Max();//最长边
					if (model.logicswayoid == 68)
					{
						var realsize = db.scb_realsize.FirstOrDefault(p => p.country == "AU" && p.cpbh == sheet.cpbh);
						if (realsize == null)
						{
							throw new Exception(string.Format("产品:{0} 未查询到真实尺寸", sheet.cpbh));
						}
						else
						{
							var realsizelist = new List<double>() { Convert.ToDouble(Math.Ceiling(Convert.ToDecimal(realsize.bzkd))), Convert.ToDouble(Math.Ceiling(Convert.ToDecimal(realsize.bzgd))), Convert.ToDouble(Math.Ceiling(Convert.ToDecimal(realsize.bzcd))) };
							var realVolume = (double)((double)realsize.bzcd * (double)realsize.bzkd * (double)realsize.bzgd / 1000000);//体积
							var maxRealSize = realsizelist.Max();

							//20231101 何雨梦 匹配，真实尺寸，允许的范围包含 临界值；
							if (maxRealSize > 105 || Weight > 22 || realVolume > 0.25)
							{
								throw new Exception(string.Format("产品:{0} 最长边:{1} 重量:{2} 体积:{3} 超过尺寸限制", sheet.cpbh, maxRealSize, Weight, realVolume));
							}
							//else if ((State == "NSW" || State == "QLD") && (sheet.cpbh.StartsWith("CM") || sheet.cpbh.StartsWith("EU")))
							//{
							//    IsAUPost = false;
							//    AUPostMsg = string.Format("产品:{0} 州:{1} 禁止匹配aupost NSW州北部铁路断了，两个州的快递都有不同程度的延误", sheet.cpbh, State);
							//}
							else
							{
								var EndDate = new DateTime(2025, 12, 25); // 2025-11-17
								if (AupostOldCpbh.Contains(sheet.cpbh) && DateTime.Now < EndDate)
								{
									var IsExtralong = maxNum >= 100 && maxNum <= 105;
									var AUPostBasic = new scb_Logistics_AUPost_EParcel();
									BillingWeight = Weight;

									var EParcels = db.Database.SqlQuery<scb_Logistics_AUPost_EParcel>(string.Format(
													@"select * from scb_Logistics_AUPost_EParcel where disable=0 and exists(
									  select 1 from scb_Logistics_AUPost_PostCode where postcode1<={0} and {0}<=postcode2 
									  and scb_Logistics_AUPost_EParcel.[DestinationZone]=scb_Logistics_AUPost_PostCode.[DestinationZone])", model.zip)).ToList();
									if (EParcels.Count > 0)
									{
										foreach (var EParcel in EParcels)
										{
											var IsFit = true;
											if (EParcel.MinWeight != null)
											{
												if ((double)EParcel.MinWeight > BillingWeight)
													IsFit = false;
											}
											if (EParcel.MaxWeight != null)
											{
												if ((double)EParcel.MaxWeight < BillingWeight)
													IsFit = false;
											}

											if (IsFit)
											{
												AUPostBasic = EParcel;
												break;
											}
										}
										var Coefficient = (AUPostBasic.Coefficient != null ? AUPostBasic.Coefficient * (decimal)BillingWeight : 0);
										//20231024 何雨梦 原来的100-113cm的超长附加费取消 从11.1开始
										//20240705 何雨梦 超长附加费还要
										//20240807 何雨梦 aupost的超长附加费 （100＜最长边≤105 收$14） 已于去年11.1之后取消了
										var Extralong = 0;//IsExtralong ? ExtralongSurcharge : 0;

										var AUPostFeeCurrent = AUPostBasic.BaseFee + Coefficient + Extralong;

										freightResult.FeeSum += (decimal)AUPostFeeCurrent * 1.07M * sheet.sl;
										//if (Order.statsa.ToUpper() == "WA")
										//{
										//    AUPostFeeSum = AUPostFeeSum * AUPostTempAddRateWA;
										//}
										// 定义目标时间
										DateTime targetDate = new DateTime(2025, 7, 1);
										// 获取当前时间
										DateTime now = DateTime.Now;
										// 比较当前时间是否大于目标时间

										if (YfFormula.Contains("*"))
										{
											YfFormula += "+";
										}
										if (now > targetDate)
										{
											freightResult.FeeSum = freightResult.FeeSum * AUPostPrice;
											if (Coefficient == 0)
											{
												YfFormula += string.Format("({0}+{1}){2}{3}{4}",
												AUPostBasic.BaseFee, Extralong, sheet.sl == 1 ? "" : "*" + sheet.sl, "*" + 1.07M, "*" + AUPostPrice);
											}
											else
											{
												YfFormula += string.Format("({0}+{1}*{2}+{3}){4}{5}{6}",
												AUPostBasic.BaseFee, AUPostBasic.Coefficient, BillingWeight, Extralong, sheet.sl == 1 ? "" : "*" + sheet.sl, "*" + 1.07M, "*" + AUPostPrice);
											}
										}
										else
										{
											if (Coefficient == 0)
											{
												YfFormula += string.Format("({0}+{1}){2}{3}{4}",
												AUPostBasic.BaseFee, Extralong, sheet.sl == 1 ? "" : "*" + sheet.sl, "*" + 1.07M, "*" + AUPostPrice);
											}
											else
											{
												YfFormula += string.Format("({0}+{1}*{2}+{3}){4}{5}{6}",
												AUPostBasic.BaseFee, AUPostBasic.Coefficient, BillingWeight, Extralong, sheet.sl == 1 ? "" : "*" + sheet.sl, "*" + 1.07M, "*" + AUPostPrice);
											}
										}

									}
									else
									{
										throw new Exception($"AUPost：发不了，邮编({model.zip})或者城市({model.city})也找不到！");
									}
								}
								else
								{
									var IsExtralong = maxNum >= 100 && maxNum <= 105;
									var AUPostBasic = new scb_Logistics_AUPost_EParcel();
									BillingWeight = Weight > Volume_WeightReal ? Weight : Volume_WeightReal;

									var EParcels = db.Database.SqlQuery<scb_Logistics_AUPost_EParcel>(string.Format(
													@"select * from scb_Logistics_AUPost_EParcel where disable=1 and exists(
									  select 1 from scb_Logistics_AUPost_PostCode where postcode1<={0} and {0}<=postcode2 
									  and scb_Logistics_AUPost_EParcel.[DestinationZone]=scb_Logistics_AUPost_PostCode.[DestinationZone])", model.zip)).ToList();
									if (EParcels.Count > 0)
									{
										foreach (var EParcel in EParcels)
										{
											var IsFit = true;
											if (EParcel.MinWeight != null)
											{
												if ((double)EParcel.MinWeight > BillingWeight)
													IsFit = false;
											}
											if (EParcel.MaxWeight != null)
											{
												if ((double)EParcel.MaxWeight < BillingWeight)
													IsFit = false;
											}

											if (IsFit)
											{
												AUPostBasic = EParcel;
												break;
											}
										}
										var Coefficient = (AUPostBasic.Coefficient != null ? AUPostBasic.Coefficient * (decimal)BillingWeight : 0);
										//20231024 何雨梦 原来的100-113cm的超长附加费取消 从11.1开始
										//20240705 何雨梦 超长附加费还要
										//20240807 何雨梦 aupost的超长附加费 （100＜最长边≤105 收$14） 已于去年11.1之后取消了
										var Extralong = 0;//IsExtralong ? ExtralongSurcharge : 0;

										var AUPostFeeCurrent = AUPostBasic.BaseFee + Coefficient + Extralong;

										freightResult.FeeSum += (decimal)AUPostFeeCurrent * AUPostRate * sheet.sl;
										//if (Order.statsa.ToUpper() == "WA")
										//{
										//    AUPostFeeSum = AUPostFeeSum * AUPostTempAddRateWA;
										//}
										// 定义目标时间
										DateTime targetDate = new DateTime(2025, 7, 1);
										// 获取当前时间
										DateTime now = DateTime.Now;
										// 比较当前时间是否大于目标时间

										if (YfFormula.Contains("*"))
										{
											YfFormula += "+";
										}
										if (now > targetDate)
										{
											freightResult.FeeSum = freightResult.FeeSum;// * AUPostPrice 20251121 加个上涨规则不要了 何雨梦
											if (Coefficient == 0)
											{
												YfFormula += string.Format("({0}+{1}){2}{3}",
												AUPostBasic.BaseFee, Extralong, sheet.sl == 1 ? "" : "*" + sheet.sl, "*" + AUPostRate);
											}
											else
											{
												YfFormula += string.Format("({0}+{1}*{2}+{3}){4}{5}",
												AUPostBasic.BaseFee, AUPostBasic.Coefficient, BillingWeight, Extralong, sheet.sl == 1 ? "" : "*" + sheet.sl, "*" + AUPostRate);
											}
										}
										else
										{
											if (Coefficient == 0)
											{
												YfFormula += string.Format("({0}+{1}){2}{3}",
												AUPostBasic.BaseFee, Extralong, sheet.sl == 1 ? "" : "*" + sheet.sl, "*" + AUPostRate);
											}
											else
											{
												YfFormula += string.Format("({0}+{1}*{2}+{3}){4}{5}",
												AUPostBasic.BaseFee, AUPostBasic.Coefficient, BillingWeight, Extralong, sheet.sl == 1 ? "" : "*" + sheet.sl, "*" + AUPostRate);
											}
										}

									}
									else
									{
										throw new Exception($"AUPost：发不了，邮编({model.zip})或者城市({model.city})也找不到！");
									}
								}
							}
						}
					}
					else if (model.logicswayoid == 69)
					{
						//计费重
						//BillingWeight = Weight > Volume_Weight ? Weight : Volume_Weight;
						//BillingWeights_AUDFE_List.Add(string.Format("{0}*{1}", BillingWeight, sheet.sl));

						//// 燃油费 * 计费重
						//DFEFeeSum += (decimal)model.RatePrice * (decimal)BillingWeight * sheet.sl;
						#region 其他附加费
						var OtherFee = 0;
						if (sizes.Sum(f => f) >= 220 || (maxNum >= 150 && maxNum < 200))
						{
							OtherFee = 8; //OtherFee = 5;
						}
						if (maxNum >= 200 && maxNum < 300)
						{
							OtherFee = 18;//OtherFee = 12;
						}
						else if (maxNum >= 300 && maxNum < 400)
						{
							OtherFee = 35;//OtherFee = 25;
						}
						else if (maxNum >= 400 && maxNum < 500)
						{
							OtherFee = 90;//OtherFee = 65;
						}
						else if (maxNum >= 500 && maxNum < 600)
						{
							OtherFee = 240;//OtherFee = 110;
						}
						else if (maxNum >= 600)
						{
							OtherFee = 460; //OtherFee = 300;
						}
						if (OtherFee > 0)
						{
							Other.Add(string.Format("{0}*{1}", OtherFee, sheet.sl));
							DFEFeeSum += OtherFee * sheet.sl;
							other += OtherFee * sheet.sl;
						}
						if (Weight > 30)//Weight >= 30 && Weight < 40
						{
							var overweightfree = Math.Ceiling(((Weight - 30) / 30)) * 7; //向上取整
							overweightfree = overweightfree > 35 ? 35 : overweightfree;
							Other.Add(string.Format("{0}*{1}", overweightfree, sheet.sl)); //Other.Add(string.Format("{0}*{1}", 5, sheet.sl));
							DFEFeeSum += 7 * sheet.sl; //AUDFEFeeSum += 5 * sheet.sl;
							other += 7 * sheet.sl;

						}

						if (Weight > 60) //else if (Weight >= 40)
						{
							Other.Add(string.Format("{0}*{1}", 45, sheet.sl));
							DFEFeeSum += 45 * sheet.sl;
							other += 45 * sheet.sl;
						}
						#endregion
					}
					else if (model.logicswayoid == 127)
					{
						// 毛重不足500g，不取整
						if (((double)good.weight / 1000) > 0.5)
						{
							BillingWeight = ((double)good.weight / 1000) > (Volume * 250) ? Weight : Volume_Weight;
						}
						else
						{
							BillingWeight = Weight;
						}

						var realsize = db.scb_realsize.FirstOrDefault(p => p.country == "AU" && p.cpbh == sheet.cpbh);
						if (realsize == null)
						{
							throw new Exception(string.Format("产品:{0} 未查询到真实尺寸", sheet.cpbh));
						}
						else
						{
							var realsizelist = new List<double>() { Convert.ToDouble(Math.Ceiling(Convert.ToDecimal(realsize.bzkd))), Convert.ToDouble(Math.Ceiling(Convert.ToDecimal(realsize.bzgd))), Convert.ToDouble(Math.Ceiling(Convert.ToDecimal(realsize.bzcd))) };
							//(double)((double)realsize.bzcd * (double)realsize.bzkd * (double)realsize.bzgd / 1000000);//体积
							var realVolume = Math.Ceiling((double)realsize.bzcd) * Math.Ceiling((double)realsize.bzkd) * Math.Ceiling((double)realsize.bzgd) / 1000000 * 250;
							var maxRealSize = realsizelist.Max();
							double chargedweight = Weight > realVolume ? Weight : realVolume;
							chargedweight = Weight < 0.5 ? chargedweight : Math.Ceiling(chargedweight); //Weight < 0.5 ? realVolume * 250 : Math.Ceiling(realVolume * 250);

							if (maxRealSize > 200 || Weight > 25 || chargedweight > 40)
							{
								throw new Exception(string.Format("产品:{0} 最长边:{1} 重量:{2} 体积重:{3} 超过尺寸限制", sheet.cpbh, maxRealSize, Weight, realVolume));
							}
							else
							{
								var aramexBasic = new scb_Logistics_aramex_EParcel();
								var EParcels = db.Database.SqlQuery<scb_Logistics_aramex_EParcel>(string.Format(
												@"select * from scb_Logistics_aramex_EParcel where zone=(select deedrf from scb_Logistics_aramex_PostCode where city='{0}' and postcode='{1}')", model.city, model.zip)).ToList();
								if (EParcels.Count > 0)
								{
									foreach (var EParcel in EParcels)
									{
										var IsFit = true;
										if (EParcel.MinWeight != null)
										{
											if ((double)EParcel.MinWeight > chargedweight)
												IsFit = false;
										}
										if (EParcel.MaxWeight != null)
										{
											if ((double)EParcel.MaxWeight < chargedweight)
												IsFit = false;
										}

										if (IsFit)
										{
											aramexBasic = EParcel;
											break;
										}
									}
									//燃油费
									//20250730 0.09->0.08 何雨梦价卡更新
									var fuel = 1M + 0.08M;
									//附加费
									//20250630 何雨梦 aramex超长附加费更新
									var additional = maxRealSize > 105 ? 15M : 0M;
									//ATL增值服务
									var atlprice = model.ATL ? 1.5M : 0M;
									if (chargedweight <= 5)
									{
										//一口价 + 燃油费 + 附加费
										freightResult.FeeSum += (decimal.Parse(aramexBasic.BaseFee.ToString()) * fuel + additional + atlprice) * sheet.sl;

										YfFormula += string.Format("({0}*1.09+{1}{2}){3}",
										aramexBasic.BaseFee, additional, model.ATL ? "+" + atlprice : "", sheet.sl == 1 ? "" : "*" + sheet.sl);
									}
									else if (chargedweight <= 25)
									{
										//(基础费率 + (计费重-5）* 每kg费率）
										var v1 = decimal.Parse((aramexBasic.BaseFee + decimal.Parse((chargedweight - 5).ToString()) * aramexBasic.Perkilo).ToString());
										freightResult.FeeSum += (v1 * fuel + additional + atlprice) * sheet.sl;

										YfFormula += string.Format("(({0}+({1}-5)*{2})*1.09 {3}){4}",
									   aramexBasic.BaseFee, chargedweight, aramexBasic.Perkilo, model.ATL ? "+" + atlprice : "", sheet.sl == 1 ? "" : "*" + sheet.sl);
									}
									else if (chargedweight <= 40)
									{
										//(基础费率 + (计费重-5）* 每kg费率）*2
										double v1 = (double.Parse(aramexBasic.BaseFee.ToString()) + (25 - 5) * double.Parse(aramexBasic.Perkilo.ToString())) * 2;
										freightResult.FeeSum += (decimal.Parse(v1.ToString()) * fuel + additional + atlprice) * sheet.sl;

										YfFormula += string.Format("(({0}+({1}-5)*{2})*2*1.09+{3}){4}",
									   aramexBasic.BaseFee, 25, aramexBasic.Perkilo, model.ATL ? "+" + atlprice : "", sheet.sl == 1 ? "" : "*" + sheet.sl);
									}
									else
									{
										throw new Exception($"计费重：{chargedweight}，超尺寸限制");
									}
								}
								else
								{
									throw new Exception($"aramex：发不了，邮编({model.zip})或者城市({model.city})也找不到！");
								}
							}
						}
					}
					else if (model.logicswayoid == 112 && model.serviceType == "TGE-Priority")
					{
						//if (model.sheets.Count > 1 && (SumWeight >= 35 || SumVolume >= 0.7))
						//{
						//    throw new Exception("一单多件总体见>=0.7立方 或者总重量>=35kg不使用TGE");
						//}
						//计费重
						BillingWeight = Weight > Volume_WeightReal ? Weight : Volume_WeightReal;
						var grith = size.bzcd + size.bzkd + size.bzgd;
						if (grith > 120 || Weight > 22)
						{
							throw new Exception(string.Format("产品:{0} 重量:{5} 长:{1} 宽:{2} 高:{3} 周长:{4} > 120 超过限制", sheet.cpbh, size.bzcd, size.bzkd, size.bzgd, grith, Weight));
						}
						TGEVolumeSum += (decimal)BillingWeight * sheet.sl;
						BillingWeights_AUDFE_List.Add($"{BillingWeight}{(sheet.sl > 1 ? ("*" + sheet.sl) : "")}");  //(string.Format("{0}*{1}", BillingWeight, sheet.sl));
					}
					else if (model.logicswayoid == 112 && model.serviceType == "TGE-IPEC")
					{
						//if (model.sheets.Count > 1 && (SumWeight >= 35 || SumVolume >= 0.7))
						//{
						//    throw new Exception("一单多件总体积>=0.7立方 或者总重量>=35kg不使用TGE");
						//}
						//计费重
						BillingWeight = Weight > Volume_WeightReal ? Weight : Volume_WeightReal;
						//var tempBillingWeight = (decimal)BillingWeight;  //计费重
						TGEVolumeSum += (decimal)BillingWeight * sheet.sl;
						BillingWeights_AUDFE_List.Add($"{BillingWeight}{(sheet.sl > 1 ? ("*" + sheet.sl) : "")}");  //(string.Format("{0}*{1}", BillingWeight, sheet.sl));
																													//var tempYfFormula = "";
						if (weight_kg_point > 40)
						{
							throw new Exception(string.Format("产品:{0} 重量:{1} 超过限制", sheet.cpbh, weight_kg_point));
						}
						var sizesArr_point = sizes_point.ToArray();
						Array.Sort(sizesArr_point);

						if (weight_kg_point >= 35 || sizesArr_point[2] > 179 || Volume > 0.7)
						{
							TGEOtherFeeSum += TGETier2Fee_IPEC * sheet.sl;//fee计算
							Other.Add($"+({TGETier2Fee_IPEC}*{sheet.sl})");
						}
						else if ((weight_kg_point > 30 && weight_kg_point < 35) || (sizesArr_point[2] > 120 && sizesArr_point[2] <= 179) || (sizesArr_point[1] > 80 && sizesArr_point[1] <= 179) || (sizesArr_point[0] > 60 && sizesArr_point[1] <= 179))
						{
							TGEOtherFeeSum += TGETier1Fee_IPEC * sheet.sl;//fee计算
							Other.Add($"+({TGETier1Fee_IPEC}*{sheet.sl})");
						}
					}
				}

				if (model.logicswayoid == 68)
				{
					//if (YfFormula.Contains("("))
					//{
					//    YfFormula += $"*{AUPostRate.ToString()}";//+ "*"+ AUPostPrice
					//}
					Result.State = true;
				}
				else if (model.logicswayoid == 69)
				{
					var BillingWeight2 = string.Empty;
					//foreach (var item in BillingWeights_AUDFE_List)
					//{
					//	if (!string.IsNullOrEmpty(BillingWeight2))
					//		BillingWeight2 += "+";
					//	BillingWeight2 += item.Replace("*1", "");
					//}

					var weight = Math.Ceiling(Weight_AUDFE_List.Sum(a => a));
					var volume = Math.Ceiling(Volume_real_AUDFE_List.Sum());

					BillingWeight2 = (weight > volume ? weight : volume).ToString();
					DFEFeeSum += model.RatePrice * Convert.ToDecimal(BillingWeight2);

					var Other2 = string.Empty;
					foreach (var item in Other)
					{
						Other2 += "+" + item.Replace("*1", "");
					}
					if (!string.IsNullOrEmpty(Other2))
					{
						Other2 = "+" + Other2;
					}


					YfFormula = string.Format("({0}+{1}*({2}){3}+{4})*{5}*{6}"
					, model.BasicCharge, model.RatePrice, BillingWeight2, Other2, model.Extralong, AUDFERate.ToString(), AUDFETotalRate.ToString());
					DFEFeeSum += model.Extralong;
					if (DFEFeeSum < model.MinimumCost)
					{
						DFEFeeSum = (decimal)model.MinimumCost;
						YfFormula += " 低于最低价(" + model.MinimumCost + ")";
					}
					DFEFeeSum = DFEFeeSum * AUDFERate * AUDFETotalRate;
					freightResult.FeeSum = DFEFeeSum;
					freightResult.other += model.Extralong;

					Result.State = true;
				}
				else if (model.logicswayoid == 127)
				{
					Result.State = true;
				}
				else if (model.logicswayoid == 112 && model.serviceType == "TGE-Priority")
				{
					var TGE_Priority_PostCodeinfo = db.Database.SqlQuery<scb_Logistics_AUTGE_Priority_PostCode>(
								$@"SELECT * FROM scb_Logistics_AUTGE_Priority_PostCode WHERE postcode = {model.zip} AND suburb = '{model.city}'")
							.FirstOrDefault();
					if (TGE_Priority_PostCodeinfo == null)
					{
						throw new Exception($"发不了，邮编({model.zip})或者城市({model.city})找不到！");
					}
					var Pri_BasicFeeInfos = db.Database.SqlQuery<scb_Logistics_AUTGE_Pri_BasicFee>(
								$@"SELECT * FROM scb_Logistics_AUTGE_Pri_BasicFee WHERE zone ='{TGE_Priority_PostCodeinfo.zone}'")
							.ToList();
					var Pri_BasicFeeInfo = new scb_Logistics_AUTGE_Pri_BasicFee();
					var currentFee = 0m;//fee计算
										//var tempBillingWeight = (decimal)freightResult.Volume_total;  //计费重
					var tempYfFormula = "";
					if (TGEVolumeSum > 5)
					{
						Pri_BasicFeeInfo = Pri_BasicFeeInfos.Where(p => p.maxWeight == 5).First();
						currentFee = Pri_BasicFeeInfo.fee + (TGEVolumeSum - 5) * Pri_BasicFeeInfo.pre_kg_fee;

						var BillingWeightYfFormula = string.Empty;
						foreach (var item in BillingWeights_AUDFE_List)
						{
							if (!string.IsNullOrEmpty(BillingWeightYfFormula))
								BillingWeightYfFormula += "+";
							BillingWeightYfFormula += item;
						}
						tempYfFormula = $"({Pri_BasicFeeInfo.fee}+(({BillingWeightYfFormula})-5)*{Pri_BasicFeeInfo.pre_kg_fee})";
					}
					else
					{
						Pri_BasicFeeInfo = Pri_BasicFeeInfos.Where(p => p.maxWeight >= TGEVolumeSum && p.minWeight <= TGEVolumeSum).First();
						currentFee = Pri_BasicFeeInfo.fee;
						tempYfFormula = $"{Pri_BasicFeeInfo.fee}";
					}
					//var Pri_RESFee = db.scb_Logistics_AUTGE_Pri_RESFee.Where(p => p.postCode == model.zip && p.suburb.ToUpper() == model.city.ToUpper()).FirstOrDefault();
					var Pri_RESFee = db.Database.SqlQuery<scb_Logistics_AUTGE_Pri_RESFee>(
								$@"SELECT * FROM scb_Logistics_AUTGE_Pri_RESFee WHERE postcode = {model.zip} AND suburb = '{model.city}'")
							.FirstOrDefault();
					if (Pri_RESFee == null)
					{
						currentFee = currentFee * TGEFLRate_Priority;
						tempYfFormula = $"{tempYfFormula}*{TGEFLRate_Priority}";
					}
					else
					{
						currentFee = currentFee * TGEFLRate_Priority + Pri_RESFee.fee;//(* AUTGETotalRate)//系数关闭
						tempYfFormula = $"{tempYfFormula}*{TGEFLRate_Priority}+({Pri_RESFee.fee})";//*{AUTGETotalRate}//系数关闭
						freightResult.other += Pri_RESFee.fee;
					}
					if (YfFormula.Contains("*"))
					{
						YfFormula += "+";
					}
					var dates = DateTime.Now;
					if ((model.statsa.ToLower() == "qld" || model.statsa.ToLower() == "queensland") && dates < new DateTime(2025, 3, 17, 0, 0, 0))
					{
						freightResult.FeeSum += (currentFee * TGEOther);
						YfFormula += $"({tempYfFormula})*{TGEOther}";
					}
					else
					{
						freightResult.FeeSum += currentFee;
						YfFormula += $"{tempYfFormula}";
					}
					Result.State = true;
				}
				else if (model.logicswayoid == 112 && model.serviceType == "TGE-IPEC")
				{
					var help = new Helper();
					var TGE_IPEC_PostCodeinfo = help.SqlQuery<scb_Logistics_AUTGE_IPEC_BasicFee>(db,
								@"select A.* from scb_Logistics_AUTGE_IPEC_BasicFee a
								left join scb_Logistics_AUTGE_IPEC_PostCode b  on a.toZone = b.zone 
								where town = {1} and postCode = {0}", model.zip, model.city.ToUpper()).FirstOrDefault();

					//db.Database.SqlQuery<scb_Logistics_AUTGE_IPEC_BasicFee>(
					//			$@"select A.* from scb_Logistics_AUTGE_IPEC_BasicFee a
					//			left join scb_Logistics_AUTGE_IPEC_PostCode b  on a.toZone = b.zone 
					//			where town = '{model.city.ToUpper()}' and postCode = {model.zip}"
					//		).FirstOrDefault();

					if (TGE_IPEC_PostCodeinfo == null)
					{
						throw new Exception($"发不了，邮编({model.zip})或者城市({model.city})找不到！");
					}
					//var tempBillingWeight = (decimal)freightResult.Volume_total;  //计费重
					var tempYfFormula = "";
					var basicfee = TGE_IPEC_PostCodeinfo.basic + TGEVolumeSum * TGE_IPEC_PostCodeinfo.per_kg_rate;
					if (basicfee < TGE_IPEC_PostCodeinfo.minCharge)
					{
						basicfee = TGE_IPEC_PostCodeinfo.minCharge;
						tempYfFormula = $"{basicfee}";
					}
					else
					{
						var BillingWeightYfFormula = string.Empty;
						foreach (var item in BillingWeights_AUDFE_List)
						{
							if (!string.IsNullOrEmpty(BillingWeightYfFormula))
								BillingWeightYfFormula += "+";
							BillingWeightYfFormula += item;
						}
						tempYfFormula = $"({TGE_IPEC_PostCodeinfo.basic} + ({BillingWeightYfFormula})*{TGE_IPEC_PostCodeinfo.per_kg_rate})";
					}
					var currentFee = basicfee * TGEFLRate_IPEC;//fee计算
					tempYfFormula += $"*{TGEFLRate_IPEC}";
					if (Other.Count > 0)
					{
						currentFee += TGEOtherFeeSum;//* AUTGETotalRate//系数关闭

						foreach (var item in Other)
						{
							tempYfFormula += item;//拼公式
						}
						//tempYfFormula += $"*{AUTGETotalRate}";//系数关闭
					}
					//var tasfee = db.scb_Logistics_AUTGE_IPEC_RESFee.Where(p => p.postcode == model.zip && p.suburb == model.city).FirstOrDefault();
					var tasfee = help.SqlQuery<scb_Logistics_AUTGE_IPEC_RESFee>(db,
								@"select * from scb_Logistics_AUTGE_IPEC_RESFee
								where suburb = {1} and postcode = {0}", model.zip, model.city.ToUpper()).FirstOrDefault();
					if (tasfee != null)
					{
						if (tasfee.fee1 > 0)
						{
							currentFee += tasfee.fee1;//fee计算//* AUTGETotalRate//系数关闭
							tempYfFormula += $"+{tasfee.fee1}";//*{AUTGETotalRate}//系数关闭
							freightResult.other += tasfee.fee1;
						}
						if (tasfee.per_kg_fee2 > 0)
						{
							currentFee += (tasfee.per_kg_fee2 * TGEVolumeSum);//fee计算//* AUTGETotalRate//系数关闭
							tempYfFormula += $"+({tasfee.per_kg_fee2}*{TGEVolumeSum})";//*{AUTGETotalRate}//系数关闭
							freightResult.other += tasfee.per_kg_fee2 * TGEVolumeSum;
						}
					}
					if (model.statsa.ToLower() == "qld" || model.statsa.ToLower() == "queensland")
					{
						var dates = DateTime.Now;
						if (dates < new DateTime(2025, 3, 17, 0, 0, 0))
						{
							YfFormula += $"({tempYfFormula})*{TGEOther}";
							freightResult.FeeSum = currentFee * TGEOther;
						}
						else if (dates > new DateTime(2025, 3, 17, 0, 0, 0) && dates < new DateTime(2025, 3, 19, 0, 0, 0)
							&& !model.city.ToLower().Contains("toowoomba") && !model.city.ToLower().Contains("sunshine coast") && !model.city.ToLower().Contains("northern rivers"))
						{
							YfFormula += $"({tempYfFormula})*{TGEOther}";
							freightResult.FeeSum = currentFee * TGEOther;
						}
						else
						{
							YfFormula += $"({tempYfFormula})";
							freightResult.FeeSum = currentFee;
						}
					}
					else
					{
						YfFormula += $"({tempYfFormula})";
						freightResult.FeeSum = currentFee;
					}
					Result.State = true;
				}

				freightResult.AUMsg = YfFormula;
				Result.FreightResult = freightResult;
				return Result;
			}
			catch (Exception ex)
			{
				Result.State = false;
				Result.Message = ex.Message;
				freightResult.AUMsg = ex.Message;
				Result.FreightResult = freightResult;
				return Result;
			}
		}
		private OrderMatchResult BaseOrderByEU(decimal number)
		{
			var OrderMatchResult = new OrderMatchResult()
			{
				SericeType = string.Empty,
				Result = new ResponseResult() { State = 0, Message = string.Empty }
			};

			Stopwatch sw = new Stopwatch();
			sw.Start();
			try
			{

				var db = new cxtradeModel();
				scb_xsjl Order = db.scb_xsjl.Find(number);

				if (Order != null)
				{
					//异常拦截
					if (IsAbnorma(Order.OrderID))
					{
						OrderMatchResult.Result.Message = "快乐星球异常拦截，请联系运营！";
					}
					else if ((Order.filterflag == 5 || Order.filterflag == 4) && Order.state == 0)
					{
						if (Order.lywl.ToUpper().Equals("SFA") && Order.temp3 == "8")
						{
							OrderMatchResult.Result.Message = string.Format("订单编号:{0}:SFA订单，请手动派单", Order.number);
						}
						else if (Order.lywl.ToUpper().Equals("SFA") && Order.temp3 != "8")
						{
							if (Order.zsl > 1)
							{
								OrderMatchResult.Result.Message = string.Format("订单编号:{0}:SFA订单商品数量大于1，请手动派单", Order.number);
							}
							else
							{
								OrderMatchResult.Result.State = 1;
								var warehouse = db.scbck.FirstOrDefault(f => f.number == Order.warehouseid);
								OrderMatchResult.WarehouseID = warehouse.number;
								OrderMatchResult.WarehouseName = warehouse.id;
								OrderMatchResult.Logicswayoid = 81;
								OrderMatchResult.SericeType = "SFA";
							}
						}
						else if (Order.temp3 == "7") //退货单
						{
							if (Order.temp10 == "DE") //德国
							{
								//发货仓库定死EU05 marl
								var sendWarehouse = new scbck
								{
									number = 61,
									name = "EU05"
								};
								#region 德国退货单处理                             
								var xsjlsheets = db.scb_xsjlsheet.Where(f => f.father == Order.number).ToList();
								if (xsjlsheets.Count > 1) //多包
								{
									OrderMatchResult.Result.Message = string.Format("订单编号:{0},多包裹请手动派单！", Order.number);
									return OrderMatchResult;
								}
								else //单包
								{
									//可以发的物流DHL、GLS
									//var logisticList = db.scb_Logisticsmode.Where(p => p.oid == 20 || p.oid == 31).ToList();

									//符合要求的物流
									List<MatchLogistic> matchLogisticList = new List<MatchLogistic>();


									var sheet = xsjlsheets.FirstOrDefault();

									#region 商品规格
									var good = db.N_goods.Where(g => g.cpbh == sheet.cpbh && g.country == "EU" && g.groupflag == 0).FirstOrDefault();
									if (good == null)
									{
										OrderMatchResult.Result.Message = string.Format("订单编号:{0},未找到商品,请联系管理员！", Order.number);
										return OrderMatchResult;
									}
									var sizes = new List<double?>();
									sizes.Add(good.bzgd);
									sizes.Add(good.bzcd);
									sizes.Add(good.bzkd);
									var maxNum = sizes.Max();//最长边 

									var Weight = Math.Ceiling((double)good.weight / 1000);//实重
									var Volume = (double)(good.bzcd * good.bzgd * good.bzkd / 1000000);//体积
																									   //var Volume_Weight = Math.Ceiling(Volume * 250);//体积重
																									   //var BillingWeight = Weight > Volume_Weight ? Weight : Volume_Weight;//计费重

									#endregion 商品规格

									#region 预设的物流费用 
									var Checkeds = db.Database.SqlQuery<OrderMatchEntity>(string.Format(
										@"select c.number WarehouseID, c.id WarehouseName,c.Origin WarehouseCountry,d.logisticsservicemode SericeType,d.logisticsid Logicswayoid,d.fee Fee from scbck c inner join OSV_EU_LogisticsForProduct d on
                            c.groupname=d.warehouse and d.goods='{0}' and IsChecked=1 and signtouse=0 and country='{1}' 
                           AND (d.LogisticsID=20 OR d.LogisticsID=31) AND  c.id='{2}'
                            where d.fee>0 ", sheet.cpbh, Order.temp10, sendWarehouse.name));
									#endregion 预设的物流费用 

									#region 计算dhl费用 
									decimal feeDHL = 0;
									var feeDHL_Model = Checkeds.Where(p => p.Logicswayoid == 20).OrderBy(p => p.Fee).FirstOrDefault();
									Helper.Info(Order.number, "wms_api", "feeDHL_Model" + JsonConvert.SerializeObject(feeDHL_Model));
									if (feeDHL_Model == null)
									{
										Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},DHL未找到物流基础价格！");
									}
									else
									{
										feeDHL = feeDHL_Model.Fee;
										Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},德国退货件,DHL物流基础价格:{feeDHL}");
										//dhl是对于长* 宽*高 <= 120 * 60 * 60的包裹，是发货运费加 + 3.96，对于长* 宽*高 > 120 * 60 * 60的包裹，是发货运费 + 23.96，如果尺寸是在“长 + 2 *（宽 + 高）<= 360cm,最长边 <= 200cm,重量 <= 31.5KG”这个范围外的不能发
										//张洁楠2022-08-25 价格有调整
										//德国DHL本地退件，对于长*宽*高<=120*60*60的包裹，是发货运费加+4.48，对于长*宽*高>120*60*60的包裹，是发货运费+24.48，尺寸限制不变
										//张洁楠2023-08-15 价格有调整
										if ((good.bzcd + 2 * (good.bzgd + good.bzkd)) > 360 || maxNum > 200 || Weight > 31.5)
										{
											Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},商品尺寸或重量不符合DHL发货要求！长：{good.bzcd}，宽：{good.bzkd}，高：{good.bzgd},重：{good.weight}");
										}
										else //可以发货计算出费用
										{
											if (good.bzcd > 120 || good.bzgd > 60 || good.bzkd > 60)
											{
												feeDHL = (decimal)24.48;
												Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},德国退货件发DHL，且物流尺寸长*宽*高>120 * 60 * 60,规定价格为{feeDHL}");
											}
											else // <= 120 * 60 * 60
											{
												feeDHL = (decimal)4.48;
												Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},德国退货件发DHL，且长*宽*高<= 120 * 60 * 60，规定价格为{feeDHL}");
											}
											matchLogisticList.Add(new MatchLogistic
											{
												SericeType = feeDHL_Model.SericeType,
												Logicswayoid = feeDHL_Model.Logicswayoid,
												Fee = feeDHL
											});
										}

									}

									#endregion

									#region 计算gls费用 
									decimal feeGLS = 0;
									var feeGLS_Model = Checkeds.Where(p => p.Logicswayoid == 31).OrderBy(p => p.Fee).FirstOrDefault();
									Helper.Info(Order.number, "wms_api", "feeGLS_Model:" + JsonConvert.SerializeObject(feeGLS_Model));
									if (feeGLS_Model == null)
									{
										Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},GLS未找到物流基础价格！");
									}
									else
									{
										feeGLS = feeGLS_Model.Fee;
										Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},德国退货件,GLS物流基础价格:{feeGLS}");
										//gls的退件费用就是发货运费 + 1.64，对于在这个尺寸范围外的“长 + 2 *（宽 + 高）<= 300cm，长* 宽*高 <= 200 * 80 * 60cm,重量 <= 40kg”不能发
										if ((good.bzcd + 2 * (good.bzgd + good.bzkd)) > 300 || (good.bzcd > 200 || good.bzgd > 80 || good.bzkd > 60) || Weight > 40)
										{
											Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},商品尺寸或重量不符合GLS发货要求！长：{good.bzcd}，宽：{good.bzkd}，高：{good.bzgd},重：{good.weight}");
										}
										else  //可以发货计算出费用
										{
											feeGLS += (decimal)1.64;
											matchLogisticList.Add(new MatchLogistic
											{
												SericeType = feeGLS_Model.SericeType,
												Logicswayoid = feeGLS_Model.Logicswayoid,
												Fee = feeGLS
											});
											Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},德国退货件发GLS需要涨价,需涨价1.64,涨价后价格为{feeGLS}");
										}
									}
									#endregion

									#region 选定最便宜的那个物流
									if (matchLogisticList != null && matchLogisticList.Count > 0)//成功匹配到物流
									{
										var cheapestLogics = matchLogisticList.OrderBy(p => p.Fee).FirstOrDefault();
										OrderMatchResult.Logicswayoid = cheapestLogics.Logicswayoid;
										OrderMatchResult.SericeType = cheapestLogics.SericeType;
										//仓库定死为marl仓
										OrderMatchResult.WarehouseID = sendWarehouse.number;
										OrderMatchResult.WarehouseName = sendWarehouse.name;
										OrderMatchResult.Result.State = 1;
									}
									else
									{
										OrderMatchResult.Result.Message = string.Format("订单编号:{0},退货单收货国家为德国,但不符合物流发货规则，请手动派单", Order.number);
										return OrderMatchResult;
									}
									#endregion 选定最便宜的那个物流 
								}
								#endregion 德国退货单处理
							}
							else if (Order.temp10 == "PL") //波兰
							{
								//发货仓库定死EU09
								var sendWarehouse = new scbck
								{
									number = 109,
									name = "EU09"
								};
								var xsjlsheets = db.scb_xsjlsheet.Where(f => f.father == Order.number).ToList();
								if (xsjlsheets.Count > 1) //多包
								{
									OrderMatchResult.Result.Message = string.Format("订单编号:{0},多包裹请手动派单！", Order.number);
									return OrderMatchResult;
								}
								else //单包
								{
									List<MatchLogistic> matchLogisticList = new List<MatchLogistic>();


									var sheet = xsjlsheets.FirstOrDefault();

									#region 物流匹配规则
									//张洁楠，2022-08-29，对于波兰本地退件的物流方式匹配规则麻烦设置为：
									//1）如果原本是用GLS发货的，仍用GLS退货
									//2）如果原本是用DPD发货的，仍用DPD退货；
									//3）除GLS、DPD外的其他物流方式发货的，一律用DPD退货。
									//原本订单
									scb_xsjl oldOrder = db.scb_xsjl.Where(p => p.OrderID == Order.OrderID && p.temp3 == "0").FirstOrDefault();
									int oldSendLogisticsID = oldOrder == null ? 31 : (int)oldOrder.logicswayoid;
									switch (oldSendLogisticsID)
									{
										case 19:
										default:
											oldSendLogisticsID = 19;
											break;
										case 31:
											oldSendLogisticsID = 31;
											break;
									}
									#endregion 物流匹配规则

									#region 预设的物流费用 
									var Checkeds = db.Database.SqlQuery<OrderMatchEntity>(string.Format(
										@"select c.number WarehouseID, c.id WarehouseName,c.Origin WarehouseCountry,d.logisticsservicemode SericeType,d.logisticsid Logicswayoid,d.fee Fee from scbck c inner join OSV_EU_LogisticsForProduct d on
                            c.groupname=d.warehouse and d.goods='{0}' and IsChecked=1 and signtouse=0 and country='{1}' 
                           AND d.LogisticsID={3} AND  c.id='{2}'
                            where d.fee>0 ", sheet.cpbh, Order.temp10, sendWarehouse.name, oldSendLogisticsID)); // and standby=0
									foreach (var item in Checkeds)
									{
										matchLogisticList.Add(new MatchLogistic
										{
											SericeType = item.SericeType,
											Logicswayoid = item.Logicswayoid,
											Fee = item.Fee
										});
									}

									#endregion 预设的物流费用 

									#region 选定最便宜的那个物流
									if (matchLogisticList != null && matchLogisticList.Count > 0)//成功匹配到物流
									{
										var cheapestLogics = matchLogisticList.OrderBy(p => p.Fee).FirstOrDefault();
										OrderMatchResult.Logicswayoid = cheapestLogics.Logicswayoid;
										OrderMatchResult.SericeType = cheapestLogics.SericeType;
										//仓库定死为marl仓
										OrderMatchResult.WarehouseID = sendWarehouse.number;
										OrderMatchResult.WarehouseName = sendWarehouse.name;
										OrderMatchResult.Result.State = 1;
									}
									else
									{
										OrderMatchResult.Result.Message = string.Format("订单编号:{0},退货单收货国家为波兰,但不符合物流发货规则，请手动派单", Order.number);
										return OrderMatchResult;
									}
									#endregion 选定最便宜的那个物流 
								}
							}
							else if (Order.temp10 == "FR") //法国
							{
								//仓库定死EU05 marl， EU10 LEHAVRE 
								var sendWarehouse_EU05_DE = new scbck
								{
									number = 61,
									name = "EU05"
								};
								var sendWarehouse_EU10_FR = new scbck
								{
									number = 106,
									name = "EU10"
								};

								#region 法国退货单处理                             
								var xsjlsheets = db.scb_xsjlsheet.Where(f => f.father == Order.number).ToList();
								if (xsjlsheets.Count > 1) //多包
								{
									OrderMatchResult.Result.Message = string.Format("订单编号:{0},多包裹请手动派单！", Order.number);
									return OrderMatchResult;
								}
								else //单包
								{
									//可以发的物流DHL、GLS
									//var logisticList = db.scb_Logisticsmode.Where(p => p.oid == 20 || p.oid == 31).ToList();

									//符合要求的物流
									List<MatchLogistic> matchLogisticList = new List<MatchLogistic>();
									var sheet = xsjlsheets.FirstOrDefault();
									#region 商品规格
									var good = db.N_goods.Where(g => g.cpbh == sheet.cpbh && g.country == "EU" && g.groupflag == 0).FirstOrDefault();
									if (good == null)
									{
										OrderMatchResult.Result.Message = string.Format("订单编号:{0},未找到商品,请联系管理员！", Order.number);
										return OrderMatchResult;
									}
									var sizes = new List<double?>();
									sizes.Add(good.bzgd);
									sizes.Add(good.bzcd);
									sizes.Add(good.bzkd);
									var maxNum = sizes.Max();//最长边 

									var Weight = Math.Ceiling((double)good.weight / 1000);//实重
									var Volume = (double)(good.bzcd * good.bzgd * good.bzkd / 1000000);//体积
																									   //var Volume_Weight = Math.Ceiling(Volume * 250);//体积重
																									   //var BillingWeight = Weight > Volume_Weight ? Weight : Volume_Weight;//计费重
									#endregion 商品规格

									#region 预设的物流费用，  规定GLS、DHL
									var Checkeds = db.Database.SqlQuery<OrderMatchEntity>(string.Format(
										@"select c.number WarehouseID, c.id WarehouseName,c.Origin WarehouseCountry,d.logisticsservicemode SericeType,d.logisticsid Logicswayoid,d.fee Fee,bzcd,bzkd,bzgd,weight from scbck c inner join OSV_EU_LogisticsForProduct d on
                            c.groupname=d.warehouse and d.goods='{0}' and IsChecked=1 and signtouse=0 and country='{1}' 
                           AND (d.LogisticsID=23 OR d.LogisticsID=31) AND  (c.id='{2}' or  c.id='{3}')
                            where d.fee>0 ", sheet.cpbh, Order.temp10, "EU05", "EU10"));  //and standby=0 
									#endregion 预设的物流费用 

									//3种情况,没有法国DHL
									#region 计算德国仓、dhl费用 
									decimal feeDHL = 0;
									var feeDHL_DE_Model = Checkeds.Where(p => p.Logicswayoid == 23 && p.WarehouseName == sendWarehouse_EU05_DE.name).OrderBy(p => p.Fee).FirstOrDefault();
									Helper.Info(Order.number, "wms_api", "feeDHL_Model" + JsonConvert.SerializeObject(feeDHL_DE_Model));
									if (feeDHL_DE_Model == null)
									{
										Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},DHL未找到物流基础价格！");
									}
									else
									{
										//DHL的国际退件的退货运费：*法国为15.11欧（尺寸限制为15 * 11 * 1cm <= 长 * 宽 * 高 <= 120 * 60 * 60，长 + 宽 + 高 <= 200cm，重量 <= 30kg，超出该尺寸限制的不可发）；
										//*法国为16.21欧，尺寸限制不变；
										//张洁楠2023-08-15 价格有调整
										feeDHL = (decimal)16.21;
										Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},法国退货件,DHL规定退货运费:{feeDHL}");
										if (((good.bzcd * good.bzgd * good.bzkd) > (120 * 60 * 60)) || ((good.bzcd + good.bzgd + good.bzkd) > 200) || Weight > 30)
										{
											Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},商品尺寸或重量不符合DHL发货要求！长：{good.bzcd}，宽：{good.bzkd}，高：{good.bzgd},重：{good.weight}");
										}
										else //可以发货计算出费用
										{
											matchLogisticList.Add(new MatchLogistic
											{
												SericeType = feeDHL_DE_Model.SericeType,
												Logicswayoid = feeDHL_DE_Model.Logicswayoid,
												WarehouseID = Convert.ToInt32(sendWarehouse_EU05_DE.number),
												WarehouseName = sendWarehouse_EU05_DE.name,
												Fee = feeDHL
											});
										}

									}

									#endregion

									#region 计算德国仓、gls费用 
									decimal feeGLS = 0;
									var feeGLS_DE_Model = Checkeds.Where(p => p.Logicswayoid == 31 && p.WarehouseName == sendWarehouse_EU05_DE.name).OrderBy(p => p.Fee).FirstOrDefault();
									Helper.Info(Order.number, "wms_api", "feeGLS_Model:" + JsonConvert.SerializeObject(feeGLS_DE_Model));
									if (feeGLS_DE_Model == null)
									{
										Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},GLS未找到物流基础价格！");
									}
									else
									{
										//尺寸限制根据理论来    0817 张洁楠
										if (good.bzkd > feeGLS_DE_Model.bzkd || good.bzcd > feeGLS_DE_Model.bzcd || good.bzgd > feeGLS_DE_Model.bzgd || Weight > feeGLS_DE_Model.weight)
										{
											Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},商品尺寸或重量不符合gls发货要求！长：{good.bzcd}，宽：{good.bzkd}，高：{good.bzgd},重：{good.weight}");
										}
										else
										{
											feeGLS = feeGLS_DE_Model.Fee;
											Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},法国退货件,德国仓GLS物流基础价格:{feeGLS}");
											feeGLS += (decimal)1.64;
											matchLogisticList.Add(new MatchLogistic
											{
												SericeType = feeGLS_DE_Model.SericeType,
												Logicswayoid = feeGLS_DE_Model.Logicswayoid,
												WarehouseID = Convert.ToInt32(sendWarehouse_EU05_DE.number),
												WarehouseName = sendWarehouse_EU05_DE.name,
												Fee = feeGLS
											});
											Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},法国退货件从德国发GLS需要涨价,需涨价1.64,涨价后价格为{feeGLS}");
										}
									}
									#endregion

									#region 计算法国仓、gls费用 
									decimal feeGLS_FR = 0;
									var feeGLS_FR_Model = Checkeds.Where(p => p.Logicswayoid == 31 && p.WarehouseName == sendWarehouse_EU10_FR.name).OrderBy(p => p.Fee).FirstOrDefault();
									Helper.Info(Order.number, "wms_api", "feeGLS_Model:" + JsonConvert.SerializeObject(feeGLS_FR_Model));
									if (feeGLS_FR_Model == null)
									{
										Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},GLS未找到物流基础价格！");
									}
									else
									{
										//尺寸限制根据理论来    0817 张洁楠
										//2023-09-15 兰地花 法国gls退件如果长宽高加起来 超过 150cm，就不退了，只能退回德国
										if (good.bzkd > feeGLS_FR_Model.bzkd || good.bzcd > feeGLS_FR_Model.bzcd || good.bzgd > feeGLS_FR_Model.bzgd || Weight > feeGLS_FR_Model.weight || (good.bzkd + good.bzcd + good.bzgd) > 150)
										{
											Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},商品尺寸或重量不符合gls发货要求！长：{good.bzcd}，宽：{good.bzkd}，高：{good.bzgd},重：{good.weight}");
										}
										else
										{
											////原本订单
											//scb_xsjl oldOrder = db.scb_xsjl.Where(p => p.OrderID == Order.OrderID && p.temp3 == "0").FirstOrDefault();
											//int zsyf = oldOrder == null ? 0 : (int)oldOrder.zsyf;
											//法国GLS本地退件的退货运费 = 发货运费 + 0.89欧  (发货运费 = 理论运费)
											//张洁楠2023-08-15 价格有调整
											feeGLS_FR = feeGLS_FR_Model.Fee + 0.89M;
											matchLogisticList.Add(new MatchLogistic
											{
												SericeType = feeGLS_FR_Model.SericeType,
												Logicswayoid = feeGLS_FR_Model.Logicswayoid,
												WarehouseID = Convert.ToInt32(sendWarehouse_EU10_FR.number),
												WarehouseName = sendWarehouse_EU10_FR.name,
												Fee = feeGLS_FR
											});
											Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},法国退货件,法国仓GLS物流退货运费=发货运费:{feeGLS_FR}");
										}
									}
									#endregion

									#region 选定最便宜的那个物流
									if (matchLogisticList != null && matchLogisticList.Count > 0)//成功匹配到物流
									{
										var cheapestLogics = matchLogisticList.OrderBy(p => p.Fee).FirstOrDefault();
										OrderMatchResult.Logicswayoid = cheapestLogics.Logicswayoid;
										OrderMatchResult.SericeType = cheapestLogics.SericeType;
										//仓库定死为marl仓
										OrderMatchResult.WarehouseID = cheapestLogics.WarehouseID;
										OrderMatchResult.WarehouseName = cheapestLogics.WarehouseName;
										OrderMatchResult.Result.State = 1;
									}
									else
									{
										OrderMatchResult.Result.Message = string.Format("订单编号:{0},退货单收货国家为法国,但不符合物流发货规则，请手动派单", Order.number);
										return OrderMatchResult;
									}
									#endregion 选定最便宜的那个物流 
								}
								#endregion 法国退货单处理
							}
							else if (Order.temp10 == "ES") //西班牙
							{
								//仓库定死EU05 marl
								var sendWarehouse = new scbck
								{
									number = 61,
									name = "EU05"
								};
								#region 西班牙退货单处理                             
								var xsjlsheets = db.scb_xsjlsheet.Where(f => f.father == Order.number).ToList();
								if (xsjlsheets.Count > 1) //多包
								{
									OrderMatchResult.Result.Message = string.Format("订单编号:{0},多包裹请手动派单！", Order.number);
									return OrderMatchResult;
								}
								else //单包
								{
									//可以发的物流DHL、GLS
									//var logisticList = db.scb_Logisticsmode.Where(p => p.oid == 20 || p.oid == 31).ToList();

									//符合要求的物流
									List<MatchLogistic> matchLogisticList = new List<MatchLogistic>();
									var sheet = xsjlsheets.FirstOrDefault();
									#region 商品规格
									var good = db.N_goods.Where(g => g.cpbh == sheet.cpbh && g.country == "EU" && g.groupflag == 0).FirstOrDefault();
									if (good == null)
									{
										OrderMatchResult.Result.Message = string.Format("订单编号:{0},未找到商品,请联系管理员！", Order.number);
										return OrderMatchResult;
									}
									var sizes = new List<double?>();
									sizes.Add(good.bzgd);
									sizes.Add(good.bzcd);
									sizes.Add(good.bzkd);
									var maxNum = sizes.Max();//最长边 

									var Weight = Math.Ceiling((double)good.weight / 1000);//实重
									var Volume = (double)(good.bzcd * good.bzgd * good.bzkd / 1000000);//体积
																									   //var Volume_Weight = Math.Ceiling(Volume * 250);//体积重
																									   //var BillingWeight = Weight > Volume_Weight ? Weight : Volume_Weight;//计费重
									#endregion 商品规格

									#region 预设的物流费用，  规定GLS、DHL
									var Checkeds = db.Database.SqlQuery<OrderMatchEntity>(string.Format(
										@"select c.number WarehouseID, c.id WarehouseName,c.Origin WarehouseCountry,d.logisticsservicemode SericeType,d.logisticsid Logicswayoid,d.fee Fee from scbck c inner join OSV_EU_LogisticsForProduct d on
                            c.groupname=d.warehouse and d.goods='{0}' and IsChecked=1 and signtouse=0 and country='{1}' 
                           AND (d.LogisticsID=23 OR d.LogisticsID=31) AND  c.id='{2}'
                            where d.fee>0 ", sheet.cpbh, Order.temp10, sendWarehouse.name));  //and standby=0 
									#endregion 预设的物流费用 

									#region 计算dhl费用 
									decimal feeDHL = 0;
									var feeDHL_Model = Checkeds.Where(p => p.Logicswayoid == 23).OrderBy(p => p.Fee).FirstOrDefault();
									Helper.Info(Order.number, "wms_api", "feeDHL_Model" + JsonConvert.SerializeObject(feeDHL_Model));
									if (feeDHL_Model == null)
									{
										Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},DHL未找到物流基础价格！");
									}
									else
									{
										//DHL的国际退件的退货运费：*西班牙为16.66欧（尺寸限制为15*11*1cm<=长*宽*高<=120*60*60，重量<=30kg，超出该尺寸限制的不可发）
										//*西班牙为17.85欧，尺寸限制不变；
										//张洁楠2023-08-15 价格有调整
										feeDHL = (decimal)17.85;
										Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},西班牙退货件,DHL规定退货运费:{feeDHL}");
										if (((good.bzcd * good.bzgd * good.bzkd) > (120 * 60 * 60)) || Weight > 30)
										{
											Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},商品尺寸或重量不符合西班牙DHL发货要求！长：{good.bzcd}，宽：{good.bzkd}，高：{good.bzgd},重：{good.weight}");
										}
										else //可以发货计算出费用
										{
											matchLogisticList.Add(new MatchLogistic
											{
												SericeType = feeDHL_Model.SericeType,
												Logicswayoid = feeDHL_Model.Logicswayoid,
												Fee = feeDHL
											});
										}

									}

									#endregion

									#region 计算gls费用 
									decimal feeGLS = 0;
									var feeGLS_Model = Checkeds.Where(p => p.Logicswayoid == 31).OrderBy(p => p.Fee).FirstOrDefault();
									Helper.Info(Order.number, "wms_api", "feeGLS_Model:" + JsonConvert.SerializeObject(feeGLS_Model));
									if (feeGLS_Model == null)
									{
										Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},GLS未找到物流基础价格！");
									}
									else
									{
										feeGLS = feeGLS_Model.Fee;
										Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},西班牙退货件,GLS物流基础价格:{feeGLS}");
										feeGLS += (decimal)1.64;
										matchLogisticList.Add(new MatchLogistic
										{
											SericeType = feeGLS_Model.SericeType,
											Logicswayoid = feeGLS_Model.Logicswayoid,
											Fee = feeGLS
										});
										Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},西班牙退货件从德国发GLS需要涨价,需涨价1.64,涨价后价格为{feeGLS}");
									}
									#endregion

									#region 选定最便宜的那个物流
									if (matchLogisticList != null && matchLogisticList.Count > 0)//成功匹配到物流
									{
										var cheapestLogics = matchLogisticList.OrderBy(p => p.Fee).FirstOrDefault();
										OrderMatchResult.Logicswayoid = cheapestLogics.Logicswayoid;
										OrderMatchResult.SericeType = cheapestLogics.SericeType;
										//仓库定死为marl仓
										OrderMatchResult.WarehouseID = sendWarehouse.number;
										OrderMatchResult.WarehouseName = sendWarehouse.name;
										OrderMatchResult.Result.State = 1;
									}
									else
									{
										OrderMatchResult.Result.Message = string.Format("订单编号:{0},退货单收货国家为西班牙,但不符合物流发货规则，请手动派单", Order.number);
										return OrderMatchResult;
									}
									#endregion 选定最便宜的那个物流 
								}
								#endregion 西班牙退货单处理
							}
							else
							{
								OrderMatchResult.Result.Message = string.Format("订单编号:{0}:非德国、波兰、法国、西班牙退件单请手动派单", Order.number);
								return OrderMatchResult;
							}
						}
						else //非退货单
						{
							MatchWarehouseByEU_V2(Order, ref OrderMatchResult);
							if (!string.IsNullOrEmpty(OrderMatchResult.WarehouseName))
							{
								//MatchLogisticsByEU(Order, ref OrderMatchResult);
								//if (!string.IsNullOrEmpty(OrderMatchResult.SericeType))
								//{

								//    FilterEU(Order, ref OrderMatchResult);

								if (!string.IsNullOrEmpty(OrderMatchResult.Result.Message))
									return OrderMatchResult;

								#region 判断当前仓库dhl数量
								////05=>07;01=>07=>05;07
								//if (OrderMatchResult.Logicswayoid == 20 || OrderMatchResult.Logicswayoid == 23)
								//{
								//    if (OrderMatchResult.WarehouseName == "EU05")
								//    {
								//        if (GetDHLCount("EU05") > 1400)
								//        {
								//            if (GetDHLCount("EU07") < 1400 && Order.temp10 != "PL" && MatchWarehouseByEU2(Order, "EU07"))
								//            {
								//                OrderMatchResult.WarehouseID = 63;
								//                OrderMatchResult.WarehouseName = "EU07";
								//            }
								//            else
								//            {
								//                MatchLogisticsSpareByEU(Order, ref OrderMatchResult);
								//            }
								//        }
								//    }
								//    else if (OrderMatchResult.WarehouseName == "EU01")
								//    {
								//        if (GetDHLCount("EU01") > 1400)
								//        {
								//            if (GetDHLCount("EU07") < 1400 && Order.temp10 != "PL" && MatchWarehouseByEU2(Order, "EU07"))
								//            {
								//                OrderMatchResult.WarehouseID = 63;
								//                OrderMatchResult.WarehouseName = "EU07";
								//            }
								//            else if (GetDHLCount("EU05") < 1400 && MatchWarehouseByEU2(Order, "EU05"))
								//            {
								//                OrderMatchResult.WarehouseID = 61;
								//                OrderMatchResult.WarehouseName = "EU05";
								//            }
								//            else
								//            {
								//                MatchLogisticsSpareByEU(Order, ref OrderMatchResult);
								//            }
								//        }
								//    }
								//    else if (OrderMatchResult.WarehouseName == "EU07")
								//    {
								//        if (GetDHLCount("EU07") > 1400)
								//            MatchLogisticsSpareByEU(Order, ref OrderMatchResult);

								//    }
								//}
								#endregion

								//}
							}
							else
							{
								return OrderMatchResult;
							}

							if (!string.IsNullOrEmpty(OrderMatchResult.WarehouseName)
								&& !string.IsNullOrEmpty(OrderMatchResult.SericeType)
								&& string.IsNullOrEmpty(OrderMatchResult.Result.Message))
								OrderMatchResult.Result.State = 1;
						}
					}
					else
					{
						OrderMatchResult.Result.Message = "订单不存在或者状态已变更，请重新查询！";
					}
				}
				else
					OrderMatchResult.Result.Message = "订单不存在或者状态已变更，请重新查询！";
			}
			catch (Exception ex)
			{
				OrderMatchResult.Result.Message = ex.Message;
				Helper.Error(number, "wms_api", ex);
			}
			sw.Stop();
			Helper.Monitor(sw.ElapsedMilliseconds, number, "BaseOrderByEU");

			return OrderMatchResult;
		}

		private OrderMatchResult BaseOrderByEUV3(EUMatchModel model)
		{
			var OrderMatchResult = new OrderMatchResult()
			{
				SericeType = string.Empty,
				Result = new ResponseResult() { State = 0, Message = string.Empty }
			};

			Stopwatch sw = new Stopwatch();
			sw.Start();
			try
			{
				var db = new cxtradeModel();
				model.StringNullToEmpty();

				//异常拦截
				if (IsAbnorma(model.OrderID))
				{
					OrderMatchResult.Result.Message = "快乐星球异常拦截，请联系运营！";
				}
				else if ((model.filterflag == 5 || model.filterflag == 4) && model.state == 0)
				{
					if (model.lywl.ToUpper().Equals("SFA") && model.temp3 == "8")
					{
						OrderMatchResult.Result.Message = string.Format("订单编号:{0}:SFA订单，请手动派单", model.number);
					}
					else if (model.lywl.ToUpper().Equals("SFA") && model.temp3 != "8")
					{
						if (model.zsl > 1)
						{
							OrderMatchResult.Result.Message = string.Format("订单编号:{0}:SFA订单商品数量大于1，请手动派单", model.number);
						}
						else
						{
							OrderMatchResult.Result.State = 1;
							var warehouse = db.scbck.FirstOrDefault(f => f.number == model.warehouseid);
							OrderMatchResult.WarehouseID = warehouse.number;
							OrderMatchResult.WarehouseName = warehouse.id;
							OrderMatchResult.Logicswayoid = 81;
							OrderMatchResult.SericeType = "SFA";
						}
					}
					else if (model.temp3 == "7") //退货单
					{
						//指定物流、仓库
						var carrier = db.scb_returnOrder_carrier.Where(p => p.orderId == model.OrderID && p.nid == model.number).FirstOrDefault();
						if (carrier != null)
						{
							if (carrier.warehouseid.HasValue && carrier.warehouseid > 0)
							{
								OrderMatchResult.WarehouseID = carrier.warehouseid.Value;
								OrderMatchResult.WarehouseName = db.scbck.First(p => p.number == OrderMatchResult.WarehouseID).id;
							}
							if (carrier.logicswayoid.HasValue && carrier.logicswayoid > 0)
							{
								OrderMatchResult.Logicswayoid = carrier.logicswayoid.Value;
								OrderMatchResult.SericeType = carrier.servicetype;
								OrderMatchResult.Result.State = 1;
							}
							return OrderMatchResult;
						}
						if (model.temp10 == "DE") //德国
						{
							//发货仓库定死EU05 marl
							var sendWarehouse = new scbck
							{
								number = 61,
								name = "EU05"
							};
							#region 德国退货单处理                             
							// var xsjlsheets = db.scb_xsjlsheet.Where(f => f.father == Order.number).ToList();
							if (model.sheets.Count > 1) //多包
							{
								OrderMatchResult.Result.Message = string.Format("订单编号:{0},多包裹请手动派单！", model.number);
								return OrderMatchResult;
							}
							else //单包
							{
								//可以发的物流DHL、GLS
								//var logisticList = db.scb_Logisticsmode.Where(p => p.oid == 20 || p.oid == 31).ToList();

								//符合要求的物流
								List<MatchLogistic> matchLogisticList = new List<MatchLogistic>();


								var sheet = model.sheets.FirstOrDefault();

								#region 商品规格
								var good = db.N_goods.Where(g => g.cpbh == sheet.cpbh && g.country == "EU" && g.groupflag == 0).FirstOrDefault();
								if (good == null)
								{
									OrderMatchResult.Result.Message = string.Format("订单编号:{0},未找到商品,请联系管理员！", model.number);
									return OrderMatchResult;
								}
								var sizes = new List<double?>();
								sizes.Add(good.bzgd);
								sizes.Add(good.bzcd);
								sizes.Add(good.bzkd);
								var maxNum = sizes.Max();//最长边 

								var Weight = Math.Ceiling((double)good.weight / 1000);//实重
								var Volume = (double)(good.bzcd * good.bzgd * good.bzkd / 1000000);//体积
																								   //var Volume_Weight = Math.Ceiling(Volume * 250);//体积重
																								   //var BillingWeight = Weight > Volume_Weight ? Weight : Volume_Weight;//计费重

								#endregion 商品规格

								#region 预设的物流费用 
								var Checkeds = db.Database.SqlQuery<OrderMatchEntity>(string.Format(
									@"select c.number WarehouseID, c.id WarehouseName,c.Origin WarehouseCountry,d.logisticsservicemode SericeType,d.logisticsid Logicswayoid,d.fee Fee from scbck c inner join OSV_EU_LogisticsForProduct d on
                            c.groupname=d.warehouse and d.goods='{0}' and IsChecked=1 and signtouse=0 and country='{1}' 
                           AND (d.LogisticsID=20 OR d.LogisticsID=31) AND  c.id='{2}'
                            where d.fee>0 ", sheet.cpbh, model.temp10, sendWarehouse.name));
								#endregion 预设的物流费用 

								#region 计算dhl费用 
								decimal feeDHL = 0;
								var feeDHL_Model = Checkeds.Where(p => p.Logicswayoid == 20).OrderBy(p => p.Fee).FirstOrDefault();
								Helper.Info(model.number, "wms_api", "feeDHL_Model" + JsonConvert.SerializeObject(feeDHL_Model));
								if (feeDHL_Model == null)
								{
									Helper.Info(model.number, "wms_api", $"订单编号:{model.number},DHL未找到物流基础价格！");
								}
								else
								{
									feeDHL = feeDHL_Model.Fee;
									Helper.Info(model.number, "wms_api", $"订单编号:{model.number},德国退货件,DHL物流基础价格:{feeDHL}");
									//dhl是对于长* 宽*高 <= 120 * 60 * 60的包裹，是发货运费加 + 3.96，对于长* 宽*高 > 120 * 60 * 60的包裹，是发货运费 + 23.96，如果尺寸是在“长 + 2 *（宽 + 高）<= 360cm,最长边 <= 200cm,重量 <= 31.5KG”这个范围外的不能发
									//张洁楠2022-08-25 价格有调整
									//德国DHL本地退件，对于长*宽*高<=120*60*60的包裹，是发货运费加+4.48，对于长*宽*高>120*60*60的包裹，是发货运费+24.48，尺寸限制不变
									//张洁楠2023-08-15 价格有调整
									if ((good.bzcd + 2 * (good.bzgd + good.bzkd)) > 360 || maxNum > 200 || Weight > 31.5)
									{
										Helper.Info(model.number, "wms_api", $"订单编号:{model.number},商品尺寸或重量不符合DHL发货要求！长：{good.bzcd}，宽：{good.bzkd}，高：{good.bzgd},重：{good.weight}");
									}
									else //可以发货计算出费用
									{
										if (good.bzcd > 120 || good.bzgd > 60 || good.bzkd > 60)
										{
											feeDHL = (decimal)24.48;
											Helper.Info(model.number, "wms_api", $"订单编号:{model.number},德国退货件发DHL，且物流尺寸长*宽*高>120 * 60 * 60,规定价格为{feeDHL}");
										}
										else // <= 120 * 60 * 60
										{
											feeDHL = (decimal)4.48;
											Helper.Info(model.number, "wms_api", $"订单编号:{model.number},德国退货件发DHL，且长*宽*高<= 120 * 60 * 60，规定价格为{feeDHL}");
										}
										matchLogisticList.Add(new MatchLogistic
										{
											SericeType = feeDHL_Model.SericeType,
											Logicswayoid = feeDHL_Model.Logicswayoid,
											Fee = feeDHL
										});
									}
								}

								#endregion

								#region 计算gls费用 
								decimal feeGLS = 0;
								var feeGLS_Model = Checkeds.Where(p => p.Logicswayoid == 31).OrderBy(p => p.Fee).FirstOrDefault();
								Helper.Info(model.number, "wms_api", "feeGLS_Model:" + JsonConvert.SerializeObject(feeGLS_Model));
								if (feeGLS_Model == null)
								{
									Helper.Info(model.number, "wms_api", $"订单编号:{model.number},GLS未找到物流基础价格！");
								}
								else
								{
									feeGLS = feeGLS_Model.Fee;
									Helper.Info(model.number, "wms_api", $"订单编号:{model.number},德国退货件,GLS物流基础价格:{feeGLS}");
									//gls的退件费用就是发货运费 + 1.64，对于在这个尺寸范围外的“长 + 2 *（宽 + 高）<= 300cm，长* 宽*高 <= 200 * 80 * 60cm,重量 <= 40kg”不能发
									if ((good.bzcd + 2 * (good.bzgd + good.bzkd)) > 300 || (good.bzcd > 200 || good.bzgd > 80 || good.bzkd > 60) || Weight > 40)
									{
										Helper.Info(model.number, "wms_api", $"订单编号:{model.number},商品尺寸或重量不符合GLS发货要求！长：{good.bzcd}，宽：{good.bzkd}，高：{good.bzgd},重：{good.weight}");
									}
									else  //可以发货计算出费用
									{
										feeGLS += (decimal)1.64;
										matchLogisticList.Add(new MatchLogistic
										{
											SericeType = feeGLS_Model.SericeType,
											Logicswayoid = feeGLS_Model.Logicswayoid,
											Fee = feeGLS
										});
										Helper.Info(model.number, "wms_api", $"订单编号:{model.number},德国退货件发GLS需要涨价,需涨价1.64,涨价后价格为{feeGLS}");
									}
								}
								#endregion

								#region 选定最便宜的那个物流
								if (matchLogisticList != null && matchLogisticList.Count > 0)//成功匹配到物流
								{
									var cheapestLogics = matchLogisticList.OrderBy(p => p.Fee).FirstOrDefault();
									OrderMatchResult.Logicswayoid = cheapestLogics.Logicswayoid;
									OrderMatchResult.SericeType = cheapestLogics.SericeType;
									//仓库定死为marl仓
									OrderMatchResult.WarehouseID = sendWarehouse.number;
									OrderMatchResult.WarehouseName = sendWarehouse.name;
									OrderMatchResult.Result.State = 1;
								}
								else
								{
									OrderMatchResult.Result.Message = string.Format("订单编号:{0},退货单收货国家为德国,但不符合物流发货规则，请手动派单", model.number);
									return OrderMatchResult;
								}
								#endregion 选定最便宜的那个物流 
							}
							#endregion 德国退货单处理
						}
						else if (model.temp10 == "PL") //波兰
						{
							//发货仓库定死EU09
							var sendWarehouse = new scbck
							{
								number = 109,
								name = "EU09"
							};
							var xsjlsheets = db.scb_xsjlsheet.Where(f => f.father == model.number).ToList();
							if (model.sheets.Count > 1) //多包
							{
								OrderMatchResult.Result.Message = string.Format("订单编号:{0},多包裹请手动派单！", model.number);
								return OrderMatchResult;
							}
							else //单包
							{
								List<MatchLogistic> matchLogisticList = new List<MatchLogistic>();


								var sheet = xsjlsheets.FirstOrDefault();

								#region 物流匹配规则
								//张洁楠，2022-08-29，对于波兰本地退件的物流方式匹配规则麻烦设置为：
								//1）如果原本是用GLS发货的，仍用GLS退货
								//2）如果原本是用DPD发货的，仍用DPD退货；
								//3）除GLS、DPD外的其他物流方式发货的，一律用DPD退货。
								//原本订单
								scb_xsjl oldOrder = db.scb_xsjl.Where(p => p.OrderID == model.OrderID && p.temp3 == "0").FirstOrDefault();
								int oldSendLogisticsID = oldOrder == null ? 31 : (int)oldOrder.logicswayoid;
								switch (oldSendLogisticsID)
								{
									case 19:
									default:
										oldSendLogisticsID = 19;
										break;
									case 31:
										oldSendLogisticsID = 31;
										break;
								}
								#endregion 物流匹配规则

								#region 预设的物流费用 
								var Checkeds = db.Database.SqlQuery<OrderMatchEntity>(string.Format(
									@"select c.number WarehouseID, c.id WarehouseName,c.Origin WarehouseCountry,d.logisticsservicemode SericeType,d.logisticsid Logicswayoid,d.fee Fee from scbck c inner join OSV_EU_LogisticsForProduct d on
                            c.groupname=d.warehouse and d.goods='{0}' and IsChecked=1 and signtouse=0 and country='{1}' 
                           AND d.LogisticsID={3} AND  c.id='{2}'
                            where d.fee>0 ", sheet.cpbh, model.temp10, sendWarehouse.name, oldSendLogisticsID)); // and standby=0
								foreach (var item in Checkeds)
								{
									matchLogisticList.Add(new MatchLogistic
									{
										SericeType = item.SericeType,
										Logicswayoid = item.Logicswayoid,
										Fee = item.Fee
									});
								}

								#endregion 预设的物流费用 

								#region 选定最便宜的那个物流
								if (matchLogisticList != null && matchLogisticList.Count > 0)//成功匹配到物流
								{
									var cheapestLogics = matchLogisticList.OrderBy(p => p.Fee).FirstOrDefault();
									OrderMatchResult.Logicswayoid = cheapestLogics.Logicswayoid;
									OrderMatchResult.SericeType = cheapestLogics.SericeType;
									//仓库定死为marl仓
									OrderMatchResult.WarehouseID = sendWarehouse.number;
									OrderMatchResult.WarehouseName = sendWarehouse.name;
									OrderMatchResult.Result.State = 1;
								}
								else
								{
									OrderMatchResult.Result.Message = string.Format("订单编号:{0},退货单收货国家为波兰,但不符合物流发货规则，请手动派单", model.number);
									return OrderMatchResult;
								}
								#endregion 选定最便宜的那个物流 
							}
						}
						else if (model.temp10 == "FR") //法国
						{
							//仓库定死EU05 marl， EU10 LEHAVRE 
							var sendWarehouse_EU05_DE = new scbck
							{
								number = 61,
								name = "EU05"
							};
							var sendWarehouse_EU10_FR = new scbck
							{
								number = 106,
								name = "EU10"
							};

							#region 法国退货单处理                             
							var xsjlsheets = db.scb_xsjlsheet.Where(f => f.father == model.number).ToList();
							if (model.sheets.Count > 1) //多包
							{
								OrderMatchResult.Result.Message = string.Format("订单编号:{0},多包裹请手动派单！", model.number);
								return OrderMatchResult;
							}
							else //单包
							{
								//可以发的物流DHL、GLS
								//var logisticList = db.scb_Logisticsmode.Where(p => p.oid == 20 || p.oid == 31).ToList();

								//符合要求的物流
								List<MatchLogistic> matchLogisticList = new List<MatchLogistic>();
								var sheet = xsjlsheets.FirstOrDefault();
								#region 商品规格
								var good = db.N_goods.Where(g => g.cpbh == sheet.cpbh && g.country == "EU" && g.groupflag == 0).FirstOrDefault();
								if (good == null)
								{
									OrderMatchResult.Result.Message = string.Format("订单编号:{0},未找到商品,请联系管理员！", model.number);
									return OrderMatchResult;
								}
								var sizes = new List<double?>();
								sizes.Add(good.bzgd);
								sizes.Add(good.bzcd);
								sizes.Add(good.bzkd);
								var maxNum = sizes.Max();//最长边 

								var Weight = Math.Ceiling((double)good.weight / 1000);//实重
								var Volume = (double)(good.bzcd * good.bzgd * good.bzkd / 1000000);//体积
																								   //var Volume_Weight = Math.Ceiling(Volume * 250);//体积重
																								   //var BillingWeight = Weight > Volume_Weight ? Weight : Volume_Weight;//计费重
								#endregion 商品规格

								#region 预设的物流费用，  规定GLS、DHL
								var Checkeds = db.Database.SqlQuery<OrderMatchEntity>(string.Format(
									@"select c.number WarehouseID, c.id WarehouseName,c.Origin WarehouseCountry,d.logisticsservicemode SericeType,d.logisticsid Logicswayoid,d.fee Fee,bzcd,bzkd,bzgd,weight from scbck c inner join OSV_EU_LogisticsForProduct d on
                            c.groupname=d.warehouse and d.goods='{0}' and IsChecked=1 and signtouse=0 and country='{1}' 
                           AND (d.LogisticsID=23 OR d.LogisticsID=31) AND  (c.id='{2}' or  c.id='{3}')
                            where d.fee>0 ", sheet.cpbh, model.temp10, "EU05", "EU10"));  //and standby=0 
								#endregion 预设的物流费用 

								//3种情况,没有法国DHL
								#region 计算德国仓、dhl费用 
								decimal feeDHL = 0;
								var feeDHL_DE_Model = Checkeds.Where(p => p.Logicswayoid == 23 && p.WarehouseName == sendWarehouse_EU05_DE.name).OrderBy(p => p.Fee).FirstOrDefault();
								Helper.Info(model.number, "wms_api", "feeDHL_Model" + JsonConvert.SerializeObject(feeDHL_DE_Model));
								if (feeDHL_DE_Model == null)
								{
									Helper.Info(model.number, "wms_api", $"订单编号:{model.number},DHL未找到物流基础价格！");
								}
								else
								{
									//DHL的国际退件的退货运费：*法国为15.11欧（尺寸限制为15 * 11 * 1cm <= 长 * 宽 * 高 <= 120 * 60 * 60，长 + 宽 + 高 <= 200cm，重量 <= 30kg，超出该尺寸限制的不可发）；
									//*法国为16.21欧，尺寸限制不变；
									//张洁楠2023-08-15 价格有调整
									feeDHL = (decimal)16.21;
									Helper.Info(model.number, "wms_api", $"订单编号:{model.number},法国退货件,DHL规定退货运费:{feeDHL}");
									if (((good.bzcd * good.bzgd * good.bzkd) > (120 * 60 * 60)) || ((good.bzcd + good.bzgd + good.bzkd) > 200) || Weight > 30)
									{
										Helper.Info(model.number, "wms_api", $"订单编号:{model.number},商品尺寸或重量不符合DHL发货要求！长：{good.bzcd}，宽：{good.bzkd}，高：{good.bzgd},重：{good.weight}");
									}
									else //可以发货计算出费用
									{
										matchLogisticList.Add(new MatchLogistic
										{
											SericeType = feeDHL_DE_Model.SericeType,
											Logicswayoid = feeDHL_DE_Model.Logicswayoid,
											WarehouseID = Convert.ToInt32(sendWarehouse_EU05_DE.number),
											WarehouseName = sendWarehouse_EU05_DE.name,
											Fee = feeDHL
										});
									}

								}

								#endregion

								#region 计算德国仓、gls费用 
								decimal feeGLS = 0;
								var feeGLS_DE_Model = Checkeds.Where(p => p.Logicswayoid == 31 && p.WarehouseName == sendWarehouse_EU05_DE.name).OrderBy(p => p.Fee).FirstOrDefault();
								Helper.Info(model.number, "wms_api", "feeGLS_Model:" + JsonConvert.SerializeObject(feeGLS_DE_Model));
								if (feeGLS_DE_Model == null)
								{
									Helper.Info(model.number, "wms_api", $"订单编号:{model.number},GLS未找到物流基础价格！");
								}
								else
								{
									//尺寸限制根据理论来    0817 张洁楠
									if (good.bzkd > feeGLS_DE_Model.bzkd || good.bzcd > feeGLS_DE_Model.bzcd || good.bzgd > feeGLS_DE_Model.bzgd || Weight > feeGLS_DE_Model.weight)
									{
										Helper.Info(model.number, "wms_api", $"订单编号:{model.number},商品尺寸或重量不符合gls发货要求！长：{good.bzcd}，宽：{good.bzkd}，高：{good.bzgd},重：{good.weight}");
									}
									else
									{
										feeGLS = feeGLS_DE_Model.Fee;
										Helper.Info(model.number, "wms_api", $"订单编号:{model.number},法国退货件,德国仓GLS物流基础价格:{feeGLS}");
										feeGLS += (decimal)1.64;
										matchLogisticList.Add(new MatchLogistic
										{
											SericeType = feeGLS_DE_Model.SericeType,
											Logicswayoid = feeGLS_DE_Model.Logicswayoid,
											WarehouseID = Convert.ToInt32(sendWarehouse_EU05_DE.number),
											WarehouseName = sendWarehouse_EU05_DE.name,
											Fee = feeGLS
										});
										Helper.Info(model.number, "wms_api", $"订单编号:{model.number},法国退货件从德国发GLS需要涨价,需涨价1.64,涨价后价格为{feeGLS}");
									}
								}
								#endregion

								#region 计算法国仓、gls费用 
								decimal feeGLS_FR = 0;
								var feeGLS_FR_Model = Checkeds.Where(p => p.Logicswayoid == 31 && p.WarehouseName == sendWarehouse_EU10_FR.name).OrderBy(p => p.Fee).FirstOrDefault();
								Helper.Info(model.number, "wms_api", "feeGLS_Model:" + JsonConvert.SerializeObject(feeGLS_FR_Model));
								if (feeGLS_FR_Model == null)
								{
									Helper.Info(model.number, "wms_api", $"订单编号:{model.number},GLS未找到物流基础价格！");
								}
								else
								{
									//尺寸限制根据理论来    0817 张洁楠
									//2023-09-15 兰地花 法国gls退件如果长宽高加起来 超过 150cm，就不退了，只能退回德国
									if (good.bzkd > feeGLS_FR_Model.bzkd || good.bzcd > feeGLS_FR_Model.bzcd || good.bzgd > feeGLS_FR_Model.bzgd || Weight > feeGLS_FR_Model.weight || (good.bzkd + good.bzcd + good.bzgd) > 150)
									{
										Helper.Info(model.number, "wms_api", $"订单编号:{model.number},商品尺寸或重量不符合gls发货要求！长：{good.bzcd}，宽：{good.bzkd}，高：{good.bzgd},重：{good.weight}");
									}
									else
									{
										////原本订单
										//scb_xsjl oldOrder = db.scb_xsjl.Where(p => p.OrderID == Order.OrderID && p.temp3 == "0").FirstOrDefault();
										//int zsyf = oldOrder == null ? 0 : (int)oldOrder.zsyf;
										//法国GLS本地退件的退货运费 = 发货运费 + 0.89欧  (发货运费 = 理论运费)
										//张洁楠2023-08-15 价格有调整
										feeGLS_FR = feeGLS_FR_Model.Fee + 0.89M;
										matchLogisticList.Add(new MatchLogistic
										{
											SericeType = feeGLS_FR_Model.SericeType,
											Logicswayoid = feeGLS_FR_Model.Logicswayoid,
											WarehouseID = Convert.ToInt32(sendWarehouse_EU10_FR.number),
											WarehouseName = sendWarehouse_EU10_FR.name,
											Fee = feeGLS_FR
										});
										Helper.Info(model.number, "wms_api", $"订单编号:{model.number},法国退货件,法国仓GLS物流退货运费=发货运费:{feeGLS_FR}");
									}
								}
								#endregion

								#region 选定最便宜的那个物流
								if (matchLogisticList != null && matchLogisticList.Count > 0)//成功匹配到物流
								{
									var cheapestLogics = matchLogisticList.OrderBy(p => p.Fee).FirstOrDefault();
									OrderMatchResult.Logicswayoid = cheapestLogics.Logicswayoid;
									OrderMatchResult.SericeType = cheapestLogics.SericeType;
									//仓库定死为marl仓
									OrderMatchResult.WarehouseID = cheapestLogics.WarehouseID;
									OrderMatchResult.WarehouseName = cheapestLogics.WarehouseName;
									OrderMatchResult.Result.State = 1;
								}
								else
								{
									OrderMatchResult.Result.Message = string.Format("订单编号:{0},退货单收货国家为法国,但不符合物流发货规则，请手动派单", model.number);
									return OrderMatchResult;
								}
								#endregion 选定最便宜的那个物流 
							}
							#endregion 法国退货单处理
						}
						else if (model.temp10 == "ES") //西班牙
						{
							//仓库定死EU05 marl
							var sendWarehouse = new scbck
							{
								number = 61,
								name = "EU05"
							};
							#region 西班牙退货单处理                             
							//var xsjlsheets = db.scb_xsjlsheet.Where(f => f.father == model.number).ToList();
							if (model.sheets.Count > 1) //多包
							{
								OrderMatchResult.Result.Message = string.Format("订单编号:{0},多包裹请手动派单！", model.number);
								return OrderMatchResult;
							}
							else //单包
							{
								//可以发的物流DHL、GLS
								//var logisticList = db.scb_Logisticsmode.Where(p => p.oid == 20 || p.oid == 31).ToList();

								//符合要求的物流
								List<MatchLogistic> matchLogisticList = new List<MatchLogistic>();
								var sheet = model.sheets.FirstOrDefault();
								#region 商品规格
								var good = db.N_goods.Where(g => g.cpbh == sheet.cpbh && g.country == "EU" && g.groupflag == 0).FirstOrDefault();
								if (good == null)
								{
									OrderMatchResult.Result.Message = string.Format("订单编号:{0},未找到商品,请联系管理员！", model.number);
									return OrderMatchResult;
								}
								var sizes = new List<double?>();
								sizes.Add(good.bzgd);
								sizes.Add(good.bzcd);
								sizes.Add(good.bzkd);
								var maxNum = sizes.Max();//最长边 

								var Weight = Math.Ceiling((double)good.weight / 1000);//实重
								var Volume = (double)(good.bzcd * good.bzgd * good.bzkd / 1000000);//体积
																								   //var Volume_Weight = Math.Ceiling(Volume * 250);//体积重
																								   //var BillingWeight = Weight > Volume_Weight ? Weight : Volume_Weight;//计费重
								#endregion 商品规格

								#region 预设的物流费用，  规定GLS、DHL
								var Checkeds = db.Database.SqlQuery<OrderMatchEntity>(string.Format(
									@"select c.number WarehouseID, c.id WarehouseName,c.Origin WarehouseCountry,d.logisticsservicemode SericeType,d.logisticsid Logicswayoid,d.fee Fee from scbck c inner join OSV_EU_LogisticsForProduct d on
                            c.groupname=d.warehouse and d.goods='{0}' and IsChecked=1 and signtouse=0 and country='{1}' 
                           AND (d.LogisticsID=23 OR d.LogisticsID=31) AND  c.id='{2}'
                            where d.fee>0 ", sheet.cpbh, model.temp10, sendWarehouse.name));  //and standby=0 
								#endregion 预设的物流费用 

								#region 计算dhl费用 
								decimal feeDHL = 0;
								var feeDHL_Model = Checkeds.Where(p => p.Logicswayoid == 23).OrderBy(p => p.Fee).FirstOrDefault();
								Helper.Info(model.number, "wms_api", "feeDHL_Model" + JsonConvert.SerializeObject(feeDHL_Model));
								if (feeDHL_Model == null)
								{
									Helper.Info(model.number, "wms_api", $"订单编号:{model.number},DHL未找到物流基础价格！");
								}
								else
								{
									//DHL的国际退件的退货运费：*西班牙为16.66欧（尺寸限制为15*11*1cm<=长*宽*高<=120*60*60，重量<=30kg，超出该尺寸限制的不可发）
									//*西班牙为17.85欧，尺寸限制不变；
									//张洁楠2023-08-15 价格有调整
									feeDHL = (decimal)17.85;
									Helper.Info(model.number, "wms_api", $"订单编号:{model.number},西班牙退货件,DHL规定退货运费:{feeDHL}");
									if (((good.bzcd * good.bzgd * good.bzkd) > (120 * 60 * 60)) || Weight > 30)
									{
										Helper.Info(model.number, "wms_api", $"订单编号:{model.number},商品尺寸或重量不符合西班牙DHL发货要求！长：{good.bzcd}，宽：{good.bzkd}，高：{good.bzgd},重：{good.weight}");
									}
									else //可以发货计算出费用
									{
										matchLogisticList.Add(new MatchLogistic
										{
											SericeType = feeDHL_Model.SericeType,
											Logicswayoid = feeDHL_Model.Logicswayoid,
											Fee = feeDHL
										});
									}

								}

								#endregion

								#region 计算gls费用 
								decimal feeGLS = 0;
								var feeGLS_Model = Checkeds.Where(p => p.Logicswayoid == 31).OrderBy(p => p.Fee).FirstOrDefault();
								Helper.Info(model.number, "wms_api", "feeGLS_Model:" + JsonConvert.SerializeObject(feeGLS_Model));
								if (feeGLS_Model == null)
								{
									Helper.Info(model.number, "wms_api", $"订单编号:{model.number},GLS未找到物流基础价格！");
								}
								else
								{
									feeGLS = feeGLS_Model.Fee;
									Helper.Info(model.number, "wms_api", $"订单编号:{model.number},西班牙退货件,GLS物流基础价格:{feeGLS}");
									feeGLS += (decimal)1.64;
									matchLogisticList.Add(new MatchLogistic
									{
										SericeType = feeGLS_Model.SericeType,
										Logicswayoid = feeGLS_Model.Logicswayoid,
										Fee = feeGLS
									});
									Helper.Info(model.number, "wms_api", $"订单编号:{model.number},西班牙退货件从德国发GLS需要涨价,需涨价1.64,涨价后价格为{feeGLS}");
								}
								#endregion

								#region 选定最便宜的那个物流
								if (matchLogisticList != null && matchLogisticList.Count > 0)//成功匹配到物流
								{
									var cheapestLogics = matchLogisticList.OrderBy(p => p.Fee).FirstOrDefault();
									OrderMatchResult.Logicswayoid = cheapestLogics.Logicswayoid;
									OrderMatchResult.SericeType = cheapestLogics.SericeType;
									//仓库定死为marl仓
									OrderMatchResult.WarehouseID = sendWarehouse.number;
									OrderMatchResult.WarehouseName = sendWarehouse.name;
									OrderMatchResult.Result.State = 1;
								}
								else
								{
									OrderMatchResult.Result.Message = string.Format("订单编号:{0},退货单收货国家为西班牙,但不符合物流发货规则，请手动派单", model.number);
									return OrderMatchResult;
								}
								#endregion 选定最便宜的那个物流 
							}
							#endregion 西班牙退货单处理
						}
						else
						{
							OrderMatchResult.Result.Message = string.Format("订单编号:{0}:非德国、波兰、法国、西班牙退件单请手动派单", model.number);
							return OrderMatchResult;
						}
					}
					else //非退货单
					{
						MatchWarehouseByEU_V3(model, ref OrderMatchResult);
						if (!string.IsNullOrEmpty(OrderMatchResult.WarehouseName))
						{
							//MatchLogisticsByEU(Order, ref OrderMatchResult);
							//if (!string.IsNullOrEmpty(OrderMatchResult.SericeType))
							//{

							//    FilterEU(Order, ref OrderMatchResult);

							if (!string.IsNullOrEmpty(OrderMatchResult.Result.Message))
								return OrderMatchResult;

							#region 判断当前仓库dhl数量
							////05=>07;01=>07=>05;07
							//if (OrderMatchResult.Logicswayoid == 20 || OrderMatchResult.Logicswayoid == 23)
							//{
							//    if (OrderMatchResult.WarehouseName == "EU05")
							//    {
							//        if (GetDHLCount("EU05") > 1400)
							//        {
							//            if (GetDHLCount("EU07") < 1400 && Order.temp10 != "PL" && MatchWarehouseByEU2(Order, "EU07"))
							//            {
							//                OrderMatchResult.WarehouseID = 63;
							//                OrderMatchResult.WarehouseName = "EU07";
							//            }
							//            else
							//            {
							//                MatchLogisticsSpareByEU(Order, ref OrderMatchResult);
							//            }
							//        }
							//    }
							//    else if (OrderMatchResult.WarehouseName == "EU01")
							//    {
							//        if (GetDHLCount("EU01") > 1400)
							//        {
							//            if (GetDHLCount("EU07") < 1400 && Order.temp10 != "PL" && MatchWarehouseByEU2(Order, "EU07"))
							//            {
							//                OrderMatchResult.WarehouseID = 63;
							//                OrderMatchResult.WarehouseName = "EU07";
							//            }
							//            else if (GetDHLCount("EU05") < 1400 && MatchWarehouseByEU2(Order, "EU05"))
							//            {
							//                OrderMatchResult.WarehouseID = 61;
							//                OrderMatchResult.WarehouseName = "EU05";
							//            }
							//            else
							//            {
							//                MatchLogisticsSpareByEU(Order, ref OrderMatchResult);
							//            }
							//        }
							//    }
							//    else if (OrderMatchResult.WarehouseName == "EU07")
							//    {
							//        if (GetDHLCount("EU07") > 1400)
							//            MatchLogisticsSpareByEU(Order, ref OrderMatchResult);

							//    }
							//}
							#endregion

							//}
						}
						else
						{
							return OrderMatchResult;
						}

						if (!string.IsNullOrEmpty(OrderMatchResult.WarehouseName)
							&& !string.IsNullOrEmpty(OrderMatchResult.SericeType)
							&& string.IsNullOrEmpty(OrderMatchResult.Result.Message))
							OrderMatchResult.Result.State = 1;
					}
				}
				else
				{
					OrderMatchResult.Result.Message = "订单不存在或者状态已变更，请重新查询！";
				}
			}
			catch (Exception ex)
			{
				OrderMatchResult.Result.Message = ex.Message;
				Helper.Error(model.number, "wms_api", ex);
			}
			sw.Stop();
			Helper.Monitor(sw.ElapsedMilliseconds, model.number, "BaseOrderByEU");

			return OrderMatchResult;
		}

		private OrderMatchResult BaseOrder(decimal number, string Date)
		{
			//var costtime = DateTime.Now;
			//var loginfo = string.Empty;

			var OrderMatchResult = new OrderMatchResult()
			{
				SericeType = string.Empty,
				Result = new ResponseResult() { State = 0, Message = string.Empty }
			};

			try
			{
				var db = new cxtradeModel();
				scb_xsjl Order = db.scb_xsjl.Find(number);
				var OrderSheets = db.scb_xsjlsheet.Where(f => f.father == number).ToList();
				var MatchRules = db.scb_WMS_MatchRule.Where(f => f.State == 1).ToList();
				if (Order != null)
				{
					if (Order.filterflag != 5 || Order.state != 0)
					{
						OrderMatchResult.Result.Message = "订单不存在或者状态已变更，请重新查询！";
						return OrderMatchResult;
					}
					var IsReturn = Order.temp6 == "12" || Order.temp3 == "7";
					if (IsReturn)
					{
						#region 退件
						var warehouse = db.scbck.FirstOrDefault(f => f.number == Order.warehouseid);
						if (warehouse == null)
						{
							var xsjl = db.scb_xsjl.FirstOrDefault(f => f.OrderID == Order.OrderID && f.state == 0 && !string.IsNullOrEmpty(f.trackno));
							warehouse = db.scbck.FirstOrDefault(f => f.number == xsjl.warehouseid);
							Order.logicswayoid = xsjl.logicswayoid;
							Order.servicetype = xsjl.servicetype;
							Order.warehouseid = xsjl.warehouseid;
						}
						if (warehouse.AreaLocation == "west" && Order.name != "wms")
						{
							OrderMatchResult.WarehouseID = 24;
							OrderMatchResult.WarehouseName = "US06";
						}
						else
						{
							OrderMatchResult.WarehouseID = (int)Order.warehouseid;
							OrderMatchResult.WarehouseName = warehouse.name;
						}

						if (Order.servicetype == "Prime")
						{
							MatchLogistics3(Order, ref OrderMatchResult);
						}
						else
						{
							OrderMatchResult.Logicswayoid = (int)Order.logicswayoid;
							OrderMatchResult.SericeType = Order.servicetype;
						}

						//fedex退件用标准服务
						if (OrderMatchResult.Logicswayoid == 18)
							OrderMatchResult.SericeType = "FEDEX_GROUND";

						//只用ups
						if (MatchRules.Any(f => f.Type == "Only_UPSGround_Send"))
						{
							var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_UPSGround_Send");
							var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
							if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == Order.dianpu.ToUpper()))
							{
								OrderMatchResult.Logicswayoid = 4;
								OrderMatchResult.SericeType = "UPSGround";
							}
						}
						//只用fedex
						if (MatchRules.Any(f => f.Type == "Only_FedexGround_Send"))
						{
							var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_FedexGround_Send");
							var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
							if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == Order.dianpu.ToUpper()))
							{
								OrderMatchResult.Logicswayoid = 18;
								OrderMatchResult.SericeType = "FEDEX_GROUND";
							}
						}

						//加拿大退到自己的仓库
						if (Order.temp10 == "CA")
						{
							OrderMatchResult.Logicswayoid = 4;
							OrderMatchResult.SericeType = "UPSInternational";
							OrderMatchResult.WarehouseID = 141;
							OrderMatchResult.WarehouseName = "US21";
						}

						if (Order.name == "wms")//第三方最后
						{
							if (OrderMatchResult.WarehouseName == "US08")
							{
								var zip = db.scb_ups_zip.FirstOrDefault(z => Order.zip.StartsWith(z.DestZip) && z.Origin == "60446");
								if (zip != null)
								{
									if (zip.TNTDAYS > 3 && Order.dianpu.ToLower() != "pioneer010".ToLower())
									{
										OrderMatchResult.SericeType = "Priority|Default|Parcel|OFF";
									}
								}
							}
							if (Order.dianpu.ToLower() == "pioneer015".ToLower())
							{
								if (OrderMatchResult.SericeType == "UPSGround" || OrderMatchResult.Logicswayoid == 4)
								{
									OrderMatchResult.Logicswayoid = 18;
									OrderMatchResult.SericeType = "FEDEX_GROUND";
								}

							}
						}

						OrderMatchResult.Result.State = 1;
						#endregion

					}
					else
					{
						//var costtime1 = DateTime.Now;
						MatchLogistics3(Order, ref OrderMatchResult);
						//loginfo += string.Format(" 物流匹配耗时:{0} ", (DateTime.Now - costtime1).TotalMilliseconds);

						//var costtime2 = DateTime.Now;
						if (string.IsNullOrEmpty(OrderMatchResult.WarehouseName))
							MatchWarehouse2(Order, Date, ref OrderMatchResult);
						//loginfo += string.Format(" 仓库匹配耗时:{0} ", (DateTime.Now - costtime2).TotalMilliseconds);

						//OrderMatchResult.Result.Message += loginfo;

						//var costtime3 = DateTime.Now;
						if (string.IsNullOrEmpty(OrderMatchResult.WarehouseName))
							return OrderMatchResult;

						#region 仓库物流禁用ups和fedex互换
						if (string.IsNullOrEmpty(Order.trackno))
						{
							if (MatchRules.Any(f => f.Type == "Ban_Fedex_ConvertUPSGround")
							&& OrderMatchResult.Logicswayoid == 18 && OrderMatchResult.SericeType == "FEDEX_GROUND")
							{
								var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Ban_Fedex_ConvertUPSGround");
								var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Warehouse").ToList();
								if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == OrderMatchResult.WarehouseName.ToUpper()))
								{
									OrderMatchResult.Logicswayoid = 4;
									OrderMatchResult.SericeType = "UPSGround";
								}
								var MatchRule_DetailsByStore = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
								if (MatchRule_DetailsByStore.Any(f => f.Parameter.ToUpper() == Order.dianpu.ToUpper()))
								{
									OrderMatchResult.Logicswayoid = 4;
									OrderMatchResult.SericeType = "UPSGround";
								}
								var MatchRule_DetailsByItem = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Item").ToList();
								if (MatchRule_DetailsByStore.Any(f => f.Parameter.ToUpper() == Order.dianpu.ToUpper()))
								{
									OrderMatchResult.Logicswayoid = 4;
									OrderMatchResult.SericeType = "UPSGround";
								}
							}
							if (MatchRules.Any(f => f.Type == "Ban_UPS_ConvertFedexGround")
								&& OrderMatchResult.Logicswayoid == 4 && OrderMatchResult.SericeType == "UPSGround")
							{
								var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Ban_UPS_ConvertFedexGround");
								var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Warehouse").ToList();
								if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == OrderMatchResult.WarehouseName.ToUpper()))
								{
									OrderMatchResult.Logicswayoid = 18;
									OrderMatchResult.SericeType = "FEDEX_GROUND";
								}
							}

							//只用ups
							if (MatchRules.Any(f => f.Type == "Only_UPSGround_Send"))
							{
								var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_UPSGround_Send");
								var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
								if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == Order.dianpu.ToUpper()))
								{
									OrderMatchResult.Logicswayoid = 4;
									OrderMatchResult.SericeType = "UPSGround";
								}
							}
							//只用fedex
							if (MatchRules.Any(f => f.Type == "Only_FedexGround_Send"))
							{
								var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_FedexGround_Send");
								var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
								if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == Order.dianpu.ToUpper()))
								{
									OrderMatchResult.Logicswayoid = 18;
									OrderMatchResult.SericeType = "FEDEX_GROUND";
								}
								var MatchRule_Details2 = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "ItemNo").ToList();
								var ItemNo = OrderSheets.FirstOrDefault().cpbh;
								if (MatchRule_Details2.Any(f => f.Parameter.ToUpper() == ItemNo.ToUpper()))
								{
									OrderMatchResult.Logicswayoid = 18;
									OrderMatchResult.SericeType = "FEDEX_GROUND";
								}
							}
						}
						#endregion

						if ("WalmartVendor,wayfair".ToUpper().Split(',').Contains(Order.dianpu.ToUpper()) && OrderMatchResult.Logicswayoid == 24)
						{
							OrderMatchResult.Logicswayoid = 4;
							OrderMatchResult.SericeType = "UPSGround";
						}
						//else if ("Sears,Sears-Gymax,Sears-Topbuy,macys,overstock-supplier,COSTWAY-GZ,COSTWAY-GZO,COSTWAY-GZOA".ToUpper().Split(',').Contains(Order.dianpu.ToUpper()))
						//{
						//    OrderMatchResult.Logicswayoid = 4;
						//    OrderMatchResult.SericeType = "UPSGround";
						//}
						//else if ("SharperImage,Topcraft-Wayfair,Topcraft-Lowes".ToUpper().Split(',').Contains(Order.dianpu.ToUpper()))
						//{
						//    OrderMatchResult.Logicswayoid = 18;
						//    OrderMatchResult.SericeType = "FEDEX_GROUND";
						//}
						else if (Order.name == "wms")//第三方最后
						{
							if (OrderMatchResult.WarehouseName == "US08")
							{
								var zip = db.scb_ups_zip.FirstOrDefault(z => Order.zip.StartsWith(z.DestZip) && z.Origin == "60446");
								if (zip != null)
								{
									if (zip.TNTDAYS > 3 && Order.dianpu.ToLower() != "pioneer010".ToLower())
									{
										OrderMatchResult.SericeType = "Priority|Default|Parcel|OFF";
									}
								}
							}
							//if (Order.dianpu.ToLower() == "pioneer015".ToLower())
							//{
							//    if (OrderMatchResult.SericeType == "UPSGround" || OrderMatchResult.Logicswayoid == 4)
							//    {
							//        OrderMatchResult.Logicswayoid = 18;
							//        OrderMatchResult.SericeType = "FEDEX_GROUND";
							//    }

							//}
						}
						if (!string.IsNullOrEmpty(OrderMatchResult.WarehouseName) && !string.IsNullOrEmpty(OrderMatchResult.SericeType))
							OrderMatchResult.Result.State = 1;
						//OrderMatchResult.Result.Message += string.Format(" 其他处理耗时:{0} ", (DateTime.Now - costtime3).TotalMilliseconds);

					}
				}
				else
					OrderMatchResult.Result.Message = "订单不存在或者状态已变更，请重新查询！";

			}
			catch (Exception ex)
			{
				OrderMatchResult.Result.Message = ex.Message;
			}
			return OrderMatchResult;
		}

		private ResponseResult US_Truck(decimal number, string Date)
		{
			var OrderMatchResult = new ResponseResult()
			{
				State = 0,
				Message = ""
			};

			try
			{
				var db = new cxtradeModel();
				scb_xsjl Order = db.scb_xsjl.Find(number);
				if (Order != null)
				{
					//异常拦截
					if (IsAbnorma(Order.OrderID))
					{
						return new ResponseResult()
						{
							State = 0,
							Message = "快乐星球异常拦截，请联系运营！"
						};
					}

					var ws = new Dictionary<decimal, List<WarehouseDetailByTruck>>();
					var datformat = Date;
					var OrderSheets = db.scb_xsjlsheet.Where(f => f.father == number).ToList();
					string PostCode = Order.zip;
					if (PostCode.Length >= 5)
						PostCode = PostCode.Substring(0, 5);

					var Warehouses = db.Database.SqlQuery<ScbckModel>(
						string.Format(@"select cast(TNTDAYS as nvarchar(50)) oids,number, a.id, name, firstsend, AreaLocation, AreaSort, a.Origin, OriginSort,0 Sort,0 Levels, 
IsMatching, IsThird, 0 EffectiveInventory, TNTDAYS c1, OriginSort c2  from scbck a left
join scb_ups_zip b on a.Origin = b.Origin and b.DestZip = '{0}'
 where countryid = '01' and a.state = 1 and  IsMatching = 1 and TNTDAYS is not null
and IsThird = 0 and IsReal = 1 order by TNTDAYS,GNDZone, OriginSort", PostCode)).ToList();

					if (Order.temp10 == "CA")
					{
						//优先加拿大仓库
						var CAWarehouses = new List<ScbckModel>();
						var scbcks = db.Database.SqlQuery<scbck>("select * from scbck where countryid = '01' and state = 1 and  IsMatching = 1 and IsThird = 0 and countrys = 'CA' order by OriginSort").ToList();
						scbcks.ForEach(x =>
						{
							CAWarehouses.Add(new ScbckModel()
							{
								id = x.id
							});
						});

						//获取全部仓库
						Warehouses = db.Database.SqlQuery<ScbckModel>(@"select 
','+isnull((select cast(oid as nvarchar(50))+',' from scb_Logisticsmode where warehoues like '%'+scbck.id+'%' and IsEnable=1
FOR XML PATH('')),'') oids,number,realname, id, name, firstsend, AreaLocation, AreaSort, Origin, OriginSort,0 Sort,0 Levels, 
IsMatching, IsThird,0 EffectiveInventory  from scbck where countryid='01' and state=1 and  IsMatching=1 order by AreaSort").ToList();

						var CA_Fedex = db.scb_WMS_MatchRule.FirstOrDefault(f => f.Type == "CA_Fedex");
						var CA_Fedexs = new List<scb_WMS_MatchRule_Details>();
						if (CA_Fedex != null)
						{
							CA_Fedexs = db.scb_WMS_MatchRule_Details.Where(f => f.RID == CA_Fedex.ID && f.Type == "Warehouse").ToList();
							Warehouses.Where(f => CA_Fedexs.Any(g => g.Parameter == f.id)).ToList().ForEach(x =>
							{
								CAWarehouses.Add(new ScbckModel()
								{
									id = x.id
								});
								Warehouses.Remove(x);
							});
						}

						//重新排序
						var WarehousesCopy = Warehouses;
						Warehouses.Clear();
						Warehouses.AddRange(CAWarehouses);
						Warehouses.AddRange(WarehousesCopy);
					}

					foreach (var item in OrderSheets)
					{
						var CurrentItemNo = item.cpbh;
						var sql = string.Format(@"select Warehouse WarehouseNo,kcsl-tempkcsl-unshipped EffectiveInventory,
'{0}' ItemNo,{1} Qty
from (select Warehouse, SUM(sl) kcsl,
isnull((select SUM(sl) tempkcsl from scb_tempkcsl where nowdate = '{2}' and ck = Warehouse and cpbh = '{0}'), 0) tempkcsl,
isnull((select SUM(qty)  from tmp_pickinglist_occupy where cangku = Warehouse and cpbh = '{0}'), 0) unshipped
from scb_kcjl_location a inner join scb_kcjl_area_cangw b
on a.Warehouse = b.Location and a.Location = b.cangw
where b.state = 0 and a.Disable = 0  and b.cpbh = '{0}' and a.warehouse like 'US%'  and  a.LocationType<10
group by Warehouse) t where kcsl - tempkcsl - unshipped >= {1}", CurrentItemNo, item.sl, datformat);
						var list = db.Database.SqlQuery<WarehouseDetailByTruck>(sql).ToList();
						ws.Add(item.number, list);

					}

					if (ws.Any())
					{
						OrderMatchResult.State = 1;
						var Baselist = ws.FirstOrDefault().Value;
						foreach (var item in ws)
						{
							Baselist = Baselist.Where(f => item.Value.Any(g => g.WarehouseNo == f.WarehouseNo)).ToList();
						}
						var Baselist2 = new List<WarehouseDetailByTruck>();
						foreach (var item in ws.Values)
						{
							Baselist2.AddRange(item.Where(f => Baselist.Any(g => g.WarehouseNo == f.WarehouseNo)));
						}
						if (Baselist2.Any())
						{
							foreach (var Warehouse in Warehouses)
							{
								var Items = Baselist2.Where(f => f.WarehouseNo == Warehouse.id).ToList();
								if (Items.Any())
								{
									string Context = string.Empty;
									foreach (var item in Items.OrderBy(f => f.ItemNo))
									{
										Context += string.Format("{0} {1}/{2}|", item.ItemNo, item.Qty, item.EffectiveInventory);
									}
									OrderMatchResult.Message += string.Format("{0}({1})\r\n ", Warehouse.id, Context.TrimEnd('|'));
								}
							}
							if (string.IsNullOrEmpty(OrderMatchResult.Message))
								OrderMatchResult.Message = "没有足够的库存";
						}
						else
						{
							foreach (var Warehouse in Warehouses)
							{
								var Items = Baselist2.Where(f => f.WarehouseNo == Warehouse.id).ToList();
								if (Items.Any())
								{
									string Context = string.Empty;
									foreach (var item in Items.OrderBy(f => f.ItemNo))
									{
										Context += string.Format("{0} {1}/{2}|", item.ItemNo, item.Qty, item.EffectiveInventory);
									}
									OrderMatchResult.Message += string.Format("{0}({1})\r\n ", Warehouse.id, Context.TrimEnd('|'));
								}
							}
							OrderMatchResult.Message = "没有足够的库存! " + OrderMatchResult.Message;
						}
					}
					else
					{
						OrderMatchResult.Message = "没有足够的库存";
					}

				}
				else
					OrderMatchResult.Message = "订单不存在或者状态已变更，请重新查询！";

			}
			catch (Exception ex)
			{
				OrderMatchResult.Message = ex.Message;
			}
			return OrderMatchResult;
		}

		private OrderMatchResult BaseOrderByUSV2(decimal number, string Date, bool ignoreAMZN = false)
		{
			var OrderMatchResult = new OrderMatchResult()
			{
				SericeType = string.Empty,
				Result = new ResponseResult() { State = 0, Message = string.Empty }
			};

			try
			{
				var db = new cxtradeModel();
				scb_xsjl Order = db.scb_xsjl.Find(number);
				var OrderSheets = db.scb_xsjlsheet.Where(f => f.father == number).ToList();
				var MatchRules = db.scb_WMS_MatchRule.Where(f => f.State == 1).ToList();
				var _UPSSkuList = LoadUPSSKusIntoMemory();
				if (Order != null)
				{
					////异常拦截
					//if (IsAbnorma(Order.OrderID))
					//{
					//	OrderMatchResult.Result.Message = "快乐星球异常拦截，请联系运营！";
					//}
					//else
					if ((Order.filterflag == 5 || Order.filterflag == 4) && Order.state == 0)
					{
						var IsReturn = Order.temp6 == "12" || Order.temp3 == "7";
						if (IsReturn)
						{
							#region 退件
							if (Order.servicetype == null)
								Order.servicetype = "";
							var warehouse = db.scbck.FirstOrDefault(f => f.number == Order.warehouseid);
							if (Order.lywl.ToUpper() != "WFS" && warehouse == null)
							{
								//2022-08-24 和史倩文协商后，退件不判断订单的货号是否一定要相同
								// var xsjl = db.scb_xsjl.FirstOrDefault(f => f.OrderID == Order.OrderID && f.state == 0 && !string.IsNullOrEmpty(f.trackno) && (f.temp3 != "7" || f.temp3 != "12"));
								//2023-02-16和史倩文协商后，恢复相同货号的判断
								var sql = string.Format(@"select x.* from scb_xsjl x inner join scb_xsjlsheet s on x.number=s.father
                                                where x.OrderID = '{0}' and x.state = 0 and x.temp3 not in ('7','12') and s.cpbh = '{1}'
                                                and ISNULL(x.trackno, '') != ''",
													Order.OrderID, OrderSheets.FirstOrDefault().cpbh);

								var xsjl = db.Database.SqlQuery<scb_xsjl>(sql).ToList().FirstOrDefault();
								if (xsjl == null)
								{
									OrderMatchResult.Result.Message = "没有原订单货号发货记录，请手动派单！";
									return OrderMatchResult;
								}
								else
								{
									warehouse = db.scbck.FirstOrDefault(f => f.number == xsjl.warehouseid);
									if (Order.servicetype.ToLower() != "fds_return")
									{
										Order.logicswayoid = xsjl.logicswayoid;
										Order.servicetype = xsjl.servicetype;
									}
									Order.warehouseid = xsjl.warehouseid;
								}
							}
							if (Order.lywl.ToUpper() != "WFS" && (Order.logicswayoid.HasValue || Order.logicswayoid.Value == 0 || string.IsNullOrEmpty(Order.servicetype) || Order.logicswayoid.Value == 37))
							{
								//var sql = string.Format(@"select x.* from scb_xsjl x inner join scb_xsjlsheet s on x.number=s.father
								//                where x.OrderID = '{0}' and x.state = 0 and x.temp3 not in ('7','12') and s.cpbh = '{1}'
								//                and ISNULL(x.trackno, '') != ''",
								//                  Order.OrderID, OrderSheets.FirstOrDefault().cpbh);

								//var xsjl = db.Database.SqlQuery<scb_xsjl>(sql).ToList().FirstOrDefault();
								var xsjl = db.scb_xsjl.FirstOrDefault(f => f.OrderID == Order.OrderID && f.state == 0 && !string.IsNullOrEmpty(f.trackno) && (f.temp3 != "7" || f.temp3 != "12"));
								if (xsjl == null)
								{
									OrderMatchResult.Result.Message = "没有原订单货号发货记录，请手动派单！";
									return OrderMatchResult;
								}
								else
								{
									if (xsjl.logicswayoid.Value == 37)
									{
										if (!xsjl.trackno.Contains("Z") && xsjl.trackno.Length == 12)
										{
											OrderMatchResult.Logicswayoid = 18;
											OrderMatchResult.SericeType = "FEDEX_GROUND";
										}
										else
										{
											OrderMatchResult.Logicswayoid = 4;
											OrderMatchResult.SericeType = "UPSGround";
										}
									}
									else
									{
										Order.logicswayoid = xsjl.logicswayoid;
										Order.servicetype = xsjl.servicetype;
										OrderMatchResult.Logicswayoid = Order.logicswayoid.Value;
										OrderMatchResult.SericeType = Order.servicetype;
									}
								}
							}
							else
							{
								OrderMatchResult.Logicswayoid = Order.logicswayoid.Value;
								OrderMatchResult.SericeType = Order.servicetype;
							}
							//2022.03.29 wjw要求 所有美国发美国的订单，退货label上面的地址全部变成us06的地址
							//if (warehouse.AreaLocation == "west" && Order.name != "wms")
							if (Order.temp10 == "US" && Order.name != "wms")
							{
								var zip = db.scb_ups_zip.Where(z => Order.zip.StartsWith(z.DestZip)).FirstOrDefault();
								if (zip != null)
								{
									switch (zip.State)
									{
										case "MN":
										case "WI":
										case "IA":
										case "IL":
										case "MO":
										case "AR":
										case "LA":
										case "MI":
										case "IN":
										case "OH":
										case "KY":
										case "TN":
										case "MS":
										case "AL":
											OrderMatchResult.WarehouseID = 44;
											OrderMatchResult.WarehouseName = "US08";
											break;
										case "GA":
										case "SC":
										case "NC":
										case "FL":
										case "NY":
										case "PA":
										case "WV":
										case "VA":
										case "ME":
										case "VT":
										case "NH":
										case "MA":
										case "CT":
										case "RI":
										case "NJ":
										case "MD":
										case "DE":
											OrderMatchResult.WarehouseID = 82;
											OrderMatchResult.WarehouseName = "US11";
											break;
										case "WA":
										case "OR":
										case "ID":
										case "MT":
										case "ND":
										case "WY":
										case "SD":
										case "CA":
										case "NV":
										case "UT":
										case "CO":
										case "AZ":
										case "NM":
										case "NE":
										case "KS":
										case "OK":
										case "TX":
											OrderMatchResult.WarehouseID = 24;
											OrderMatchResult.WarehouseName = "US06";
											break;

										default:
											OrderMatchResult.WarehouseID = 24;
											OrderMatchResult.WarehouseName = "US06";
											break;
									}
								}
								else
								{
									OrderMatchResult.WarehouseID = 24;
									OrderMatchResult.WarehouseName = "US06";
								}


								//switch (warehouse.id)
								//{
								//    case "US06":
								//    case "US07":
								//    case "US10":
								//    case "US12":
								//    case "US17":
								//        OrderMatchResult.WarehouseID = 24;
								//        OrderMatchResult.WarehouseName = "US06";
								//        break;
								//    case "US04":
								//    case "US08":
								//    case "US09":
								//    case "US11":
								//        OrderMatchResult.WarehouseID = 44;
								//        OrderMatchResult.WarehouseName = "US08";
								//        break;
								//    default:
								//        OrderMatchResult.WarehouseID = 24;
								//        OrderMatchResult.WarehouseName = "US06";
								//        break;
								//}

							}
							else if (Order.temp10 == "CA" && Order.name != "wms")
							{
								OrderMatchResult.WarehouseID = 141;
								OrderMatchResult.WarehouseName = "US21";
							}
							else
							{
								OrderMatchResult.WarehouseID = (int)Order.warehouseid;
								OrderMatchResult.WarehouseName = warehouse.name;
							}

							if (Order.lywl.ToUpper() == "WFS")
							{
								var sheets = db.scb_xsjlsheet.Where(f => f.father == Order.number).ToList();
								double weight = 0;
								double weight_g = 0;
								double? volNum = 0;
								double? volNum2 = 0;
								var sizes = new List<double?>();
								foreach (var sheet in sheets)
								{
									var good = db.N_goods.Where(g => g.cpbh == sheet.cpbh && g.country == Order.country && g.groupflag == 0).FirstOrDefault();
									sizes.Add(good.bzgd);
									sizes.Add(good.bzcd);
									sizes.Add(good.bzkd);
									weight += (double)good.weight_express * sheet.sl / 1000 * 2.2046226;
									weight_g += (double)good.weight_express * sheet.sl;
									volNum += (good.bzcd + 2 * (good.bzkd + good.bzgd)) * sheet.sl;
									volNum2 += good.bzcd * good.bzgd * good.bzkd * sheet.sl;
									var sizesum = sizes.Sum(f => f) * 2 - sizes.Max();
								}
								double weight_vol = (double)volNum2 / 250;
								double weight_phy = weight_vol > weight ? weight_vol : weight;
								var maxNum = sizes.Max();
								var secNum = sizes.Where(f => f < maxNum).Max();
								if (sizes.Count(f => f < maxNum) <= sizes.Count - 2)
								{
									secNum = maxNum;
								}
								var configs = db.scb_WMS_LogisticsForWeight.Where(f => f.IsChecked == 1).ToList();
								var skModel = new ScbckModel()
								{
									id = OrderMatchResult.WarehouseName
								};
								WarehouseLevel(skModel, _UPSSkuList, configs, MatchRules, Order, sheets, weight, weight_g, volNum, volNum2, weight_vol, weight_phy, maxNum, secNum, Order.temp10, false, ref OrderMatchResult, false);
							}
							else if (Order.servicetype == "Prime" || Order.logicswayoid == 37)
							{
								MatchLogistics2(Order, ref OrderMatchResult);
							}
							else if (Order.logicswayoid.Value == 79)
							{
								OrderMatchResult.Logicswayoid = 18;
								OrderMatchResult.SericeType = "FEDEX_GROUND";
							}
							else if (Order.servicetype.Contains("Pioneer") || Order.logicswayoid == 59)
							{
								if (Order.servicetype.ToLower() != "fds_return")
								{
									OrderMatchResult.Logicswayoid = 18;
									OrderMatchResult.SericeType = "FEDEX_GROUND";
								}
								else
								{
									//自物流退货单优先最高
									OrderMatchResult.Logicswayoid = (int)Order.logicswayoid;
									OrderMatchResult.SericeType = Order.servicetype;
									OrderMatchResult.Result.State = 1;
									#region 墨西哥物流指定
									if (Order.temp10.ToUpper() == "MX")
									{
										OrderMatchResult.Logicswayoid = 48;
										OrderMatchResult.SericeType = "UPSInternational";
									}
									#endregion 墨西哥物流指定
									return OrderMatchResult;
								}
							}
							else
							{
								OrderMatchResult.Logicswayoid = (int)Order.logicswayoid;
								OrderMatchResult.SericeType = Order.servicetype;
							}
							//prime补充
							if (OrderMatchResult.Logicswayoid == 37)
							{
								if (OrderMatchResult.SericeType.ToLower().Contains("fedex"))
								{
									OrderMatchResult.Logicswayoid = 18;
									OrderMatchResult.SericeType = "FEDEX_GROUND";
								}
								else if (OrderMatchResult.SericeType.ToLower().Contains("upsground"))
								{
									OrderMatchResult.Logicswayoid = 4;
									OrderMatchResult.SericeType = "UPSGround";
								}

							}
							//fedex退件用标准服务
							if (OrderMatchResult.Logicswayoid == 18)
								OrderMatchResult.SericeType = "FEDEX_GROUND";

							//只用ups
							if (MatchRules.Any(f => f.Type == "Only_UPSGround_Send"))
							{
								var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_UPSGround_Send");
								var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
								if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == Order.dianpu.ToUpper()))
								{
									OrderMatchResult.Logicswayoid = 4;
									OrderMatchResult.SericeType = "UPSGround";
								}
							}
							//只用fedex
							if (MatchRules.Any(f => f.Type == "Only_FedexGround_Send"))
							{
								var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_FedexGround_Send");
								var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
								if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == Order.dianpu.ToUpper()))
								{
									OrderMatchResult.Logicswayoid = 18;
									OrderMatchResult.SericeType = "FEDEX_GROUND";
								}
							}

							//加拿大退到自己的仓库
							if (Order.temp10 == "CA")
							{
								OrderMatchResult.Logicswayoid = 18;
								OrderMatchResult.SericeType = "FEDEX_GROUND";
								OrderMatchResult.WarehouseID = 141;
								OrderMatchResult.WarehouseName = "US21";
							}

							if (Order.name == "wms")//第三方最后
							{
								if (OrderMatchResult.WarehouseName == "US08")
								{
									var zip = db.scb_ups_zip.FirstOrDefault(z => Order.zip.StartsWith(z.DestZip) && z.Origin == "60446");
									if (zip != null)
									{
										if (zip.TNTDAYS > 3 && Order.dianpu.ToLower() != "pioneer010".ToLower())
										{
											OrderMatchResult.SericeType = "Priority|Default|Parcel|OFF";
										}
									}
								}
								if (Order.dianpu.ToLower() == "pioneer015".ToLower())
								{
									if (OrderMatchResult.SericeType == "UPSGround" || OrderMatchResult.Logicswayoid == 4)
									{
										OrderMatchResult.Logicswayoid = 18;
										OrderMatchResult.SericeType = "FEDEX_GROUND";
									}

								}
							}
							else
							{
								//同订单多个退件物流不同就用Fedex
								var return_orders = db.Database.SqlQuery<string>(string.Format(@"select distinct servicetype from scb_xsjl 
                        where OrderID = '{0}' and state = 0 and temp3 in ('7', '12') and filterflag<=11
                        and number != {1}", Order.OrderID, Order.number)).ToList();
								if (return_orders.Count > 1)
								{
									//if (return_orders.Distinct().Count() > 1)
									//{
									OrderMatchResult.Logicswayoid = 18;
									OrderMatchResult.SericeType = "FEDEX_GROUND";
									//}
								}

								var return_Fedex_orders = db.Database.SqlQuery<string>(string.Format(@"select servicetype from scb_xsjl 
                        where OrderID = '{0}' and state = 0 and temp3 in ('7', '12') and filterflag<=11
                        and number != {1} and logicswayoid=18", Order.OrderID, Order.number)).ToList();
								if (return_Fedex_orders.Count > 0)
								{
									//if (return_orders.Distinct().Count() > 1)
									//{
									OrderMatchResult.Logicswayoid = 18;
									OrderMatchResult.SericeType = "FEDEX_GROUND";
									//}
								}
							}

							//20231013 史倩文  所有return label的物流麻烦都调整成fedex
							OrderMatchResult.Logicswayoid = 18;
							OrderMatchResult.SericeType = "FEDEX_GROUND";

							//指定物流、仓库
							var carrier = db.scb_returnOrder_carrier.Where(p => p.orderId == Order.OrderID && p.nid == Order.number).FirstOrDefault();
							if (carrier != null)
							{
								if (carrier.warehouseid.HasValue && carrier.warehouseid > 0)
								{
									OrderMatchResult.WarehouseID = carrier.warehouseid.Value;
									OrderMatchResult.WarehouseName = db.scbck.First(p => p.number == OrderMatchResult.WarehouseID).id;
								}
								if (carrier.logicswayoid.HasValue && carrier.logicswayoid > 0)
								{
									OrderMatchResult.Logicswayoid = carrier.logicswayoid.Value;
									OrderMatchResult.SericeType = carrier.servicetype;
									if (OrderMatchResult.SericeType == "UPS" && carrier.logicswayoid == 4)
									{
										OrderMatchResult.SericeType = "UPSGround";
									}
								}
							}

							OrderMatchResult.Result.State = 1;

							#endregion
						}
						else
						{
							#region 发货
							foreach (var item in OrderSheets)
							{
								if (item.cpbh.ToLower().Equals("seel-bp17"))
								{
									string upcMsg = "SEEL-BP17请直接转至全部订单！";
									Helper.Info(Order.number, "wms_api", upcMsg);
									OrderMatchResult.Result.Message = upcMsg;
									return OrderMatchResult;

								}
							}
							#region  Target-topbuy 店铺
							if (Order.dianpu.ToUpper() == "TARGET-TOPBUY")
							{
								string upc_code = "";
								foreach (var item in OrderSheets)
								{
									var ca_sku_upc = db.ca_Sku_Upc.Where(d => d.Sku == item.sku && d.StoreName.ToUpper() == "TARGET-TOPBUY" && d.Upc != "").FirstOrDefault();
									if (ca_sku_upc == null)
									{
										string upcMsg = "Target-topbuy店铺订单sku在Ca_Sku_Upc查询失败，无法匹配！请先在“快乐星球”系统里做sku与upc的对应关系！";
										Helper.Info(Order.number, "wms_api", upcMsg);
										OrderMatchResult.Result.Message = upcMsg;
										return OrderMatchResult;
									}
									var scb_target_tcin = db.scb_target_tcin.Where(p => p.PartnerSKU == item.sku && p.shop.ToUpper() == "TARGET-TOPBUY" && p.TCIN != "").FirstOrDefault();
									if (scb_target_tcin == null)
									{
										string tcinMsg = "Target-topbuy店铺订单sku在scb_target_tcin查询失败，无法匹配！请先在“快乐星球”系统里做sku与tcin的对应关系！";
										Helper.Info(Order.number, "wms_api", tcinMsg);
										OrderMatchResult.Result.Message = tcinMsg;
										return OrderMatchResult;
									}
								}
							}
							#endregion

							#region  Target店铺
							if (Order.dianpu.ToUpper() == "TARGET")
							{
								string upc_code = "";
								foreach (var item in OrderSheets)
								{
									var ca_sku_upc = db.ca_Sku_Upc.Where(d => d.Sku == item.sku && d.StoreName.ToUpper() == "TARGET" && d.Upc != "").FirstOrDefault();
									if (ca_sku_upc == null)
									{
										string upcMsg = "Target店铺订单sku在Ca_Sku_Upc查询失败，无法匹配！请先在“快乐星球”系统里做sku与upc的对应关系！";
										Helper.Info(Order.number, "wms_api", upcMsg);
										OrderMatchResult.Result.Message = upcMsg;
										return OrderMatchResult;
									}
									var scb_target_tcin = db.scb_target_tcin.Where(p => p.PartnerSKU == item.sku && p.shop.ToUpper() == "TARGET" && p.TCIN != "").FirstOrDefault();
									if (scb_target_tcin == null)
									{
										string tcinMsg = "Target店铺订单sku在scb_target_tcin查询失败，无法匹配！请先在“快乐星球”系统里做sku与tcin的对应关系！";
										Helper.Info(Order.number, "wms_api", tcinMsg);
										OrderMatchResult.Result.Message = tcinMsg;
										return OrderMatchResult;
									}
								}
							}
							#endregion
							MatchLogisticsByUSV2(Order, _UPSSkuList, Date, ref OrderMatchResult, ignoreAMZN);


							//20240507 US仓库不用zpl
							//var CanZpl = CanSendByZPL(Order.number, OrderSheets.FirstOrDefault().cpbh, OrderMatchResult.WarehouseName, OrderMatchResult.Logicswayoid);
							OrderMatchResult.CanZPL = false;//CanZpl;
							if (string.IsNullOrEmpty(OrderMatchResult.WarehouseName))
							{
								#region 墨西哥物流指定
								if (Order.temp10.ToUpper() == "MX")
								{
									OrderMatchResult.Logicswayoid = 48;
									OrderMatchResult.SericeType = "UPSInternational";
								}
								#endregion 墨西哥物流指定
								return OrderMatchResult;
							}
							//2023-01-29 target不发surepost，改发UPS或FedEx
							if (Order.xspt.ToLower() == "target" && OrderMatchResult.Logicswayoid == 24)
							{
								OrderMatchResult.Logicswayoid = 4;
								OrderMatchResult.SericeType = "UPSGround";
							}
							//2023-10-13 walmart想先不发surepost 改发FedEx
							if (Order.dianpu.ToLower() == "walmart" && OrderMatchResult.Logicswayoid == 24)
							{
								OrderMatchResult.Logicswayoid = 18;
								OrderMatchResult.SericeType = "FEDEX_GROUND";
							}

							#region 三方特殊匹配
							else if (Order.name == "wms")//第三方最后
							{
								if (OrderMatchResult.WarehouseName == "US08")
								{
									var zip = db.scb_ups_zip.FirstOrDefault(z => Order.zip.StartsWith(z.DestZip) && z.Origin == "60446");
									if (zip != null)
									{
										if (zip.TNTDAYS > 3 && Order.dianpu.ToLower() != "pioneer010".ToLower())
										{
											OrderMatchResult.SericeType = "Priority|Default|Parcel|OFF";
										}
									}
								}
							}
							#endregion

							#region 亚马逊primeday(弃用)
							else if ((Order.xspt.ToLower() == "am" || Order.xspt.ToLower() == "amazon") && false)
							{
								if (Order.orderdate >= new DateTime(2021, 06, 21) && Order.orderdate < new DateTime(2021, 06, 23) && Order.temp10 == "US")
								{
									if (OrderMatchResult.Logicswayoid == 24)
									{
										OrderMatchResult.Logicswayoid = 4;
										OrderMatchResult.SericeType = "UPSGround";
									}
									else if (OrderMatchResult.Logicswayoid == 3)
									{

									}
									else
									{
										double? size = 0D;
										foreach (var sheet in OrderSheets)
										{
											var good = db.scb_realsize.Where(g => g.cpbh == sheet.cpbh && g.country == "US").FirstOrDefault();
											size += (good.bzcd + 2 * (good.bzkd + good.bzgd)) * sheet.sl;
										}
										if (size > 130)
										{
											var cc = db.Database.SqlQuery<int>(string.Format(
												@"select count(1) from [dbo].[scb_WMS_PrimeDayMatch] where shop='{0}' and itemno='{1}'",
												Order.dianpu, OrderSheets.FirstOrDefault().cpbh)).ToList().FirstOrDefault();
											if (cc > 0)
											{
												OrderMatchResult.Logicswayoid = 4;
												OrderMatchResult.SericeType = "UPSGround";
											}
											else
											{
												OrderMatchResult.Logicswayoid = 18;
												OrderMatchResult.SericeType = "FEDEX_GROUND";
											}
										}
										else
										{
											OrderMatchResult.Logicswayoid = 4;
											OrderMatchResult.SericeType = "UPSGround";
										}
									}
								}
							}
							#endregion

							#region 店铺target us06,us11发ups 禁用
							//else if (Order.dianpu.ToLower() == "target")
							//{
							//    if (OrderMatchResult.WarehouseName == "US06" || OrderMatchResult.WarehouseName == "US11")
							//    {
							//        OrderMatchResult.Logicswayoid = 4;
							//        OrderMatchResult.SericeType = "UPSGround";
							//    }
							//}
							#endregion


							#region CA BC只发Fedex

							else if (OrderMatchResult.WarehouseName == "US121" && OrderMatchResult.SericeType == "UPSInternational"
							   && (Order.statsa == "BC" || Order.statsa == "British Columbia"))
							{
								OrderMatchResult.Logicswayoid = 18;
								OrderMatchResult.SericeType = "FEDEX_GROUND";
							}
							else if (OrderMatchResult.WarehouseName == "US21" && OrderMatchResult.SericeType == "UPSInternational"
								&& (Order.statsa == "BC" || Order.statsa == "British Columbia"))
							{
								OrderMatchResult.Logicswayoid = 18;
								OrderMatchResult.SericeType = "FEDEX_GROUND";
							}

							#endregion

							#region CA Wayfair调整物流名字
							if (Order.xspt.ToLower() == "wayfair" && Order.temp10.ToUpper() == "CA" && (OrderMatchResult.WarehouseName == "US17" || OrderMatchResult.WarehouseName == "US16" || OrderMatchResult.WarehouseName == "US06" || OrderMatchResult.WarehouseName == "US11"))
							{
								OrderMatchResult.SericeType = "UPSInternational";
							}


							#endregion

							#region CA UPSInternational 改成UPS
							if ((OrderMatchResult.WarehouseName == "US121" || OrderMatchResult.WarehouseName == "US21") && OrderMatchResult.SericeType == "UPSInternational")
							{

								var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_FedexGround_Send");
								var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
								if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == Order.dianpu.ToUpper()))
								{
									OrderMatchResult.Logicswayoid = 18;
									OrderMatchResult.SericeType = "FEDEX_GROUND";
								}
								else
								{
									OrderMatchResult.Logicswayoid = 4;
									OrderMatchResult.SericeType = "UPSGround";
								}

							}

							#endregion

							#region CA UPSInternational 改成UPSToCA和FedexToCA
							if ((OrderMatchResult.WarehouseName == "US17") && OrderMatchResult.SericeType == "UPSInternational" && Order.temp10.ToUpper() == "CA")
							{
								OrderMatchResult.Logicswayoid = 80;
								OrderMatchResult.SericeType = "UPSToCA";
							}
							//20240416 张洁楠 从4.17开始，对于从US06/US16发出的订单，Giantex-CA店铺+DORTALA-CA店铺这两个店铺的订单改发UPStoCA
							//20240424 张洁楠 Giantex-CA和KOTEKUS-CA 这两个店铺的订单发FedExtoCA
							if ((OrderMatchResult.WarehouseName == "US16" || OrderMatchResult.WarehouseName == "US06") && OrderMatchResult.SericeType == "UPSInternational" && Order.temp10.ToUpper() == "CA")
							{
								var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_FedexGround_Send");
								var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();

								if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == Order.dianpu.ToUpper())
									|| Order.xspt.ToLower().Equals("walmart") || Order.xspt.ToLower().Equals("wayfair") || Order.dianpu.ToLower().Equals("tangkula-ca")
									|| Order.dianpu.ToLower().Equals("giantex-ca") || Order.dianpu.ToUpper().Equals("KOTEKUS-CA"))
								{
									OrderMatchResult.Logicswayoid = 78;
									OrderMatchResult.SericeType = "FedexToCA";
								}
								else
								{
									OrderMatchResult.Logicswayoid = 80;
									OrderMatchResult.SericeType = "UPSToCA";
								}
							}
							#endregion

							#region US21特殊规则
							//20240527 张洁楠 这个只调整这一周的 下周开始正常
							var dat = DateTime.Now;
							if (Order.temp10 == "CA" && dat < new DateTime(2024, 5, 30, 0, 0, 0, 0) && OrderMatchResult.WarehouseName == "US21" && OrderMatchResult.SericeType.Contains("UPS") && "0,1,6".Contains(dat.DayOfWeek.ToString("d")))
							{
								try
								{
									var startTime = dat;
									var endTime = dat;
									if (dat.DayOfWeek == DayOfWeek.Saturday)
									{
										startTime = dat.Date;
										endTime = dat.Date.AddDays(3);
									}
									else if (dat.DayOfWeek == DayOfWeek.Sunday)
									{
										startTime = dat.Date.AddDays(-1);
										endTime = dat.Date.AddDays(2);
									}
									else if (dat.DayOfWeek == DayOfWeek.Monday)
									{
										startTime = dat.Date.AddDays(-2);
										endTime = dat.Date.AddDays(1);
									}
									var qty1 = db.Database.SqlQuery<int>(string.Format($@"select isnull(SUM(x.zsl),0) from scb_xsjl x 
								                where country = 'US' and x.temp10 = 'CA'
								                and x.state = 0 and filterflag > 5 AND x.newdateis>'{startTime.ToString("yyyy-MM-dd")}' AND x.newdateis<'{endTime.ToString("yyyy-MM-dd")}'
												AND x.hthm NOT LIKE 'Order%' AND x.hthm NOT LIKE 'return%'AND x.logicswayoid in (4) and x.warehouseid in (141)")).ToList().FirstOrDefault();
									if (qty1 > 1000)
									{
										OrderMatchResult.Logicswayoid = 18;
										OrderMatchResult.SericeType = "FEDEX_GROUND";
									}
								}
								catch (Exception ex)
								{
									OrderMatchResult.Logicswayoid = 18;
									OrderMatchResult.SericeType = "FEDEX_GROUND";
								}
							}

							#endregion


							#region US11 特殊规则
							if ((OrderMatchResult.WarehouseName == "US11") && OrderMatchResult.SericeType == "UPSInternational" && Order.temp10.ToUpper() == "CA")
							{
								#region 注释 20240424 张洁楠 US11只发UPS，能用FedEx发的货号这一个规则就取消，尺寸规则也不要了
								//对于目的地在YT、NT、NU的订单指定用FedEx发
								//var CANoUPS = MatchRules.FirstOrDefault(f => f.Type == "CANoUPS");

								//var CANoUPSs = db.scb_WMS_MatchRule_Details.Where(f => f.RID == CANoUPS.ID && f.Type == "ZIP").ToList();
								//if (CANoUPSs.Any(f => Order.zip.StartsWith(f.Parameter)))
								//{
								//	OrderMatchResult.Logicswayoid = 78;
								//	OrderMatchResult.SericeType = "FedexToCA";
								//}
								//else
								//{
								//	//必须要用Fedex发货
								//	var US11MustFedexGood = MatchRules.FirstOrDefault(f => f.Type == "US11MustFedexGood");
								//	var US11MustFedexGood_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == US11MustFedexGood.ID).Select(o => o.Parameter).ToList();
								//	var exsitUS11MustFedexGood_Details = OrderSheets.Where(o => US11MustFedexGood_Details.Contains(o.cpbh)).Any();

								//	var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_FedexGround_Send");
								//	var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();


								//	if (exsitUS11MustFedexGood_Details || MatchRule_Details.Any(f => f.Parameter.ToUpper() == Order.dianpu.ToUpper()))
								//	{
								//		OrderMatchResult.Logicswayoid = 78;
								//		OrderMatchResult.SericeType = "FedexToCA";
								//	}
								//	else
								//	{
								//		if (DateTime.Now.ToString("yyyy-MM-dd") == "2022-12-24")
								//		{
								//			OrderMatchResult.Logicswayoid = 80;
								//			OrderMatchResult.SericeType = "UPSToCA";
								//		}
								//		else
								//		{
								//			//周一到周四打单打FedEx，周五周六打单打UPS，且UPS的单量两天加起来不超过650单
								//			//2022-11-15改成周一到周2打单打FedEx，其他打UPS，且fedex的单量两天加起来不超过2600单
								//			var dat2 = DateTime.Now;
								//			var dw = dat2.DayOfWeek.ToString("d");

								//			//8.6开始，Borden仓发出的加拿大订单，周三-周五发FedEx to ca，且单量控制在1200单内，多余的订单以及周一、二、六、日的订单都发UPS to ca.

								//			if ("2,3,4,5".Contains(dw))
								//			{
								//				try
								//				{
								//					OrderMatchResult.Logicswayoid = 78;
								//					OrderMatchResult.SericeType = "FedexToCA";
								//					#region 注释

								//					var startTime = DateTime.Now;
								//					var endTime = DateTime.Now;
								//					if (DateTime.Now.DayOfWeek == DayOfWeek.Tuesday)
								//					{
								//						startTime = DateTime.Now.Date;
								//						endTime = DateTime.Now.Date.AddDays(4);
								//					}
								//					else if (DateTime.Now.DayOfWeek == DayOfWeek.Wednesday)
								//					{
								//						startTime = DateTime.Now.AddDays(-1);
								//						endTime = DateTime.Now.Date.AddDays(3);
								//					}
								//					else if (DateTime.Now.DayOfWeek == DayOfWeek.Thursday)
								//					{
								//						startTime = DateTime.Now.Date.AddDays(-2);
								//						endTime = DateTime.Now.Date.AddDays(2);
								//					}
								//					else if (DateTime.Now.DayOfWeek == DayOfWeek.Friday)
								//					{
								//						startTime = DateTime.Now.Date.AddDays(-3);
								//						endTime = DateTime.Now.Date.AddDays(1);
								//					}
								//					var qty1 = db.Database.SqlQuery<int>(string.Format($@"select isnull(SUM(s.sl),0) from scb_xsjl x inner join scb_xsjlsheet s on 
								//                                                x.number = s.father
								//                                                where country = 'US'
								//                                                and x.state = 0 and filterflag > 5 AND x.newdateis>'{startTime.ToString("yyyy-MM-dd")}' AND x.newdateis<'{endTime.ToString("yyyy-MM-dd")}' AND x.hthm NOT LIKE 'Order%' AND x.hthm NOT LIKE 'return%'AND x.logicswayoid in (78) and x.warehouseid in (82)")).ToList().FirstOrDefault();
								//					//20231009 张洁楠 Borden的FedExtoCA 周二-周五不能超过1000单
								//					//20231016 张洁楠 US11的FedExtoCA麻烦设置成不超过1200单，时间还是不变
								//					//20240416 张洁楠 Borden仓发出的FedExtoCA周二-周五的单量控制在1000单内
								//					if (qty1 <= 1000)  //1200
								//					{
								//						OrderMatchResult.Logicswayoid = 78;
								//						OrderMatchResult.SericeType = "FedexToCA";
								//					}
								//					else
								//					{
								//						OrderMatchResult.Logicswayoid = 80;
								//						OrderMatchResult.SericeType = "UPSToCA";
								//					}

								//					#endregion
								//				}
								//				catch (Exception ex)
								//				{
								//					OrderMatchResult.Logicswayoid = 78;
								//					OrderMatchResult.SericeType = "FedexToCA";
								//				}
								//			}
								//			else
								//			{
								//				OrderMatchResult.Logicswayoid = 80;
								//				OrderMatchResult.SericeType = "UPSToCA";
								//			}


								//		}
								//		//对于实重>90磅或者体积重>63磅的货必须用FedEx发，其中体积重按照长*宽*高/320去计算
								//		double? volNum2 = 0;
								//		double weight = 0;
								//		foreach (var sheet in OrderSheets)
								//		{
								//			var good = db.N_goods.Where(g => g.cpbh == sheet.cpbh && g.country == Order.country && g.groupflag == 0).FirstOrDefault();
								//			weight += (double)good.weight_express * sheet.sl / 1000 * 2.2046226;
								//			volNum2 += good.bzcd * good.bzgd * good.bzkd * sheet.sl;
								//		}
								//		var ups_weight = volNum2 / 320;
								//		if (weight > 90 || ups_weight > 63)
								//		{
								//			OrderMatchResult.Logicswayoid = 78;
								//			OrderMatchResult.SericeType = "FedexToCA";
								//		}
								//	}
								//}
								#endregion

								OrderMatchResult.Logicswayoid = 80;
								OrderMatchResult.SericeType = "UPSToCA";
							}
							#endregion

							if ((Order.dianpu.ToLower() == "arnot" || Order.dianpu.ToLower() == "wellhut") && OrderMatchResult.Logicswayoid == 24)
							{
								OrderMatchResult.Logicswayoid = 4;
								OrderMatchResult.SericeType = "UPSGround";
							}
							//刘志明店铺，只用Fedex
							if (Order.dianpu == "Costway-PhilipCA")
							{
								if (OrderMatchResult.Logicswayoid == 80)
								{
									OrderMatchResult.Logicswayoid = 78;
									OrderMatchResult.SericeType = "FedexToCA";
								}
								if (OrderMatchResult.Logicswayoid == 4)
								{
									OrderMatchResult.Logicswayoid = 18;
									OrderMatchResult.SericeType = "FEDEX_GROUND";
								}

							}
							if (Order.dianpu.ToLower() == "overstock-ca")
							{
								if (OrderMatchResult.Logicswayoid == 78)
								{
									OrderMatchResult.Logicswayoid = 80;
									OrderMatchResult.SericeType = "UPSToCA";
								}
								if (OrderMatchResult.Logicswayoid == 18)
								{
									OrderMatchResult.Logicswayoid = 4;
									OrderMatchResult.SericeType = "UPSGround";
								}
							}
							#region 墨西哥物流指定
							if (Order.temp10.ToUpper() == "MX")
							{
								OrderMatchResult.Logicswayoid = 48;
								OrderMatchResult.SericeType = "UPSInternational";
							}
							#endregion 墨西哥物流指定

							if (Order.temp10 == "CA")//FedEx
							{
								int[] fedex_oidList = new int[] { 18, 58, 78, 79 };
								int[] ups_oidList = new int[] { 4, 44, 47, 75, 80 };//UPSInternational除外
								if (fedex_oidList.Contains(OrderMatchResult.Logicswayoid))
								{
									var MatchRuleCA = MatchRules.FirstOrDefault(f => f.Type == "Remote_DistrictsByCA");
									var MatchRule_DetailsCA = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRuleCA.ID && f.Type == "Zip").ToList();
									if (MatchRule_DetailsCA.Any(f => Order.zip.StartsWith(f.Parameter)) &&
										MatchRuleCA.Shop.ToUpper().Split(',').Contains(Order.dianpu.ToUpper()))
									{
										OrderMatchResult.Result.Message = string.Format("匹配错误,加拿大特殊邮编[{0}]禁止FedEx发货！订单编号:{1}", Order.zip, Order.number);
										return OrderMatchResult;
									}
								}
								else if (ups_oidList.Contains(OrderMatchResult.Logicswayoid))
								{
									var MatchRuleCA = MatchRules.FirstOrDefault(f => f.Type == "Remote_DistrictsByCA");
									string sqlAccord = $"SELECT * FROM [dbo].[scb_WMS_MatchRule_Details_CA_UPS] WHERE '{Order.zip}'>=PostalCodeFrom AND '{Order.zip}'<=PostalCodeTo";
									var listAccord = db.Database.SqlQuery<scb_WMS_MatchRule_Details_CA_UPS>(sqlAccord).ToList();
									if (listAccord.Any() &&
										MatchRuleCA.Shop.ToUpper().Split(',').Contains(Order.dianpu.ToUpper()))
									{
										OrderMatchResult.Result.Message = string.Format("匹配错误,加拿大特殊邮编[{0}]禁止UPS发货！订单编号:{1}", Order.zip, Order.number);
										return OrderMatchResult;
									}
								}
							}

							#region  US21、US121 CA发US，禁用UPS
							//OrderMatch.WarehouseName == "US21" || OrderMatch.WarehouseName == "US121") && Tocountry == "US"
							if ((OrderMatchResult.WarehouseName == "US21" || OrderMatchResult.WarehouseName == "US121") && Order.temp10 == "US" && OrderMatchResult.SericeType.Contains("UPS"))
							{
								OrderMatchResult.Result.Message = "CA发US，禁用UPS！";
								OrderMatchResult.WarehouseName = "";
								OrderMatchResult.SericeType = "";
							}
							#endregion



							//20231103 wjf US10禁用UPS
							if (OrderMatchResult.WarehouseName == "US10" && OrderMatchResult.SericeType.Contains("UPS"))
							{
								if (db.scb_order_carrier.Any(p => p.OrderID == Order.OrderID && p.dianpu == Order.dianpu && p.oid.HasValue) || Order.temp10 != "US")
								{
									OrderMatchResult.Result.Message = "US10禁用UPS！";
									OrderMatchResult.WarehouseName = "";
									OrderMatchResult.SericeType = "";
								}
								else
								{
									OrderMatchResult.Logicswayoid = 18;
									OrderMatchResult.SericeType = "FEDEX_GROUND";
								}
							}

							//20240321 wjf temu禁用Fedex
							//20240418 wjf temu取消禁用Fedex
							//20240423 wjf temu禁用UPS
							//20240423 wjf temu 美国禁ups 取消，不属于ups可发平台，所以不用禁也不会发UPS
							//if (Order.xspt.ToLower() == "temu" && OrderMatchResult.SericeType.ToLower().Contains("ups") && Order.temp10 == "US")
							//{
							//	OrderMatchResult.Logicswayoid = 18;
							//	OrderMatchResult.SericeType = "FEDEX_GROUND";

							//	//if (OrderMatchResult.WarehouseName == "US10")
							//	//{
							//	//	OrderMatchResult.Result.Message = $"平台{Order.xspt} 禁发Fedex！";
							//	//	OrderMatchResult.WarehouseName = "";
							//	//	OrderMatchResult.SericeType = "";
							//	//}
							//	//else
							//	//{
							//	//	OrderMatchResult.Logicswayoid = 4;
							//	//	OrderMatchResult.SericeType = "UPSGround";
							//	//}
							//}


							//if (Order.temp10 == "US" && "US06,US11".Split(',').Contains(OrderMatchResult.WarehouseName) && OrderMatchResult.SericeType.ToLower().Contains("fedex"))
							//{
							//	var cpbhs = OrderSheets.Select(p => p.cpbh).ToList();
							//	var goods = db.N_goods.Where(p => p.country == "US" && cpbhs.Contains(p.cpbh));
							//	var maxNum = goods.Max(p => p.bzcd);
							//	double? volNum = 0;
							//	double weight_bl = 0;
							//	foreach (var item in OrderSheets)
							//	{
							//		var good = goods.First(p => p.cpbh == item.cpbh);
							//		volNum += (good.bzcd + 2 * (good.bzkd + good.bzgd)) * item.sl;
							//		weight_bl += (double)good.weight_express * item.sl / 1000 * 2.2046226;
							//	}
							//	if (maxNum > 96 || (volNum > 130 && volNum <= 165 && weight_bl < 150))
							//	{
							//		OrderMatchResult.SericeType = "FedEx-3rd";
							//	}
							//}

							if (!string.IsNullOrEmpty(OrderMatchResult.WarehouseName) && !string.IsNullOrEmpty(OrderMatchResult.SericeType))
								OrderMatchResult.Result.State = 1;


							#endregion
						}
					}
					else
					{
						OrderMatchResult.Result.Message = "订单不存在或者状态已变更，请重新查询！";
						return OrderMatchResult;
					}
				}
			}
			catch (Exception ex)
			{
				OrderMatchResult.Result.Message = ex.Message;
				Helper.Error(number, "wms_api", ex);
			}
			return OrderMatchResult;
		}

		private OrderMatchResult BaseOrderByAU(decimal number)
		{
			var OrderMatchResult = new OrderMatchResult()
			{
				SericeType = string.Empty,
				Result = new ResponseResult() { State = 0, Message = string.Empty }
			};
			var theory_fee = 0m;
			var customPay_fee = 0m;
			var match_fee = 0m;
			//附加费
			var other = 0m;

			try
			{
				var db = new cxtradeModel();
				scb_xsjl Order = db.scb_xsjl.Find(number);
				if (Order != null)
				{
					//异常拦截
					if (IsAbnorma(Order.OrderID))
					{
						OrderMatchResult.Result.Message = "快乐星球异常拦截，请联系运营！";
					}
					else if (Order.filterflag != 1)//&& Order.state == 0
					{

						if ((Order.adress.ToLower().Contains("30 distribution drive") || Order.adress.ToLower().Contains("30 distribution dri") || Order.address1.ToLower().Contains("30 distribution drive") || Order.address1.ToLower().Contains("30 distribution dri")) && Order.zip == "3029" && Order.city.ToLower() == "truganina")
						{
							return new OrderMatchResult()
							{
								SericeType = string.Empty,
								Result = new ResponseResult() { State = 0, Message = "匹配失败。此单为仓库地址，无法发货，请确认！" }
							};
						}


						var IsReturn = Order.temp6 == "12" || Order.temp3 == "7";
						if (IsReturn)
						{
							MatchReturnByAU(Order, ref OrderMatchResult);
							//OrderMatchResult.Result.Message = string.Format("订单编号:{0}:澳洲暂不支持退件自动匹配", Order.number);
						}
						else
						{
							//MatchWarehouseByAU(Order, ref OrderMatchResult, ref theory_fee, ref customPay_fee, ref match_fee);
							MatchWarehouseByAUV2(Order, ref OrderMatchResult, ref theory_fee, ref customPay_fee, ref match_fee, ref other);
						}
						if (OrderMatchResult.Result.State == 1)
						{
							OrderMatchResult.MatchFee = match_fee;
							var freightLossFee = theory_fee + customPay_fee - match_fee;
							if (freightLossFee <= -40)
							{
								OrderMatchResult.Result.State = 0;
								OrderMatchResult.Result.Message += $"匹配失败，运费亏损大于40; 物流:{OrderMatchResult.SericeType} 目的地附加费:{other};";
								Helper.Info(Order.number, "wms_api", $"{freightLossFee}(运费亏损)={theory_fee}(理论运费)+{customPay_fee}(客户支付)-{match_fee}(匹配运费)");
							}
						}
						if (!string.IsNullOrEmpty(OrderMatchResult.WarehouseName))
						{
							if (OrderMatchResult.Result.State == 1)
								return OrderMatchResult;
							if (!string.IsNullOrEmpty(OrderMatchResult.Result.Message))
								return OrderMatchResult;
						}
						else
						{
							return OrderMatchResult;
						}

						if (!string.IsNullOrEmpty(OrderMatchResult.WarehouseName)
							&& !string.IsNullOrEmpty(OrderMatchResult.SericeType)
							&& string.IsNullOrEmpty(OrderMatchResult.Result.Message))
							OrderMatchResult.Result.State = 1;
					}
					else
					{
						OrderMatchResult.Result.Message = "订单不存在或者状态已变更，请重新查询！";
					}

				}
				else
					OrderMatchResult.Result.Message = "订单不存在或者状态已变更，请重新查询！";

			}
			catch (Exception ex)
			{
				OrderMatchResult.Result.Message = ex.Message;
			}
			return OrderMatchResult;
		}

		private OrderMatchResult BaseOrderByGB(decimal number)
		{
			var OrderMatchResult = new OrderMatchResult()
			{
				SericeType = string.Empty,
				Result = new ResponseResult() { State = 0, Message = string.Empty }
			};
			try
			{
				var db = new cxtradeModel();
				scb_xsjl Order = db.scb_xsjl.Find(number);
				if (Order != null)
				{
					//异常拦截
					if (IsAbnorma(Order.OrderID))
					{
						OrderMatchResult.Result.Message = "快乐星球异常拦截，请联系运营！";
					}
					else if ((Order.filterflag == 5 || Order.filterflag == 4) && Order.state == 0)
					{
						if (!CheckByGB(Order, ref OrderMatchResult))
						{ return OrderMatchResult; }
						else if (Order.lywl.ToUpper().Equals("SFA"))
						{
							if (Order.zsl > 1)
							{
								OrderMatchResult.Result.Message = string.Format("订单编号:{0}:SFA订单商品数量大于1，请手动派单", Order.number);
							}

							else
							{

								//OrderMatchResult.Result.State = 1;
								//OrderMatchResult.WarehouseID = 83;
								//OrderMatchResult.WarehouseName = "GB06";
								//OrderMatchResult.Logicswayoid = 81;
								//OrderMatchResult.SericeType = "SFA";

								#region SFA  
								List<OrderMatchEntityGB> entityGBs = new List<OrderMatchEntityGB>();
								var sheets = db.scb_xsjlsheet.Where(f => f.father == Order.number).ToList();
								var CurrentItemNo = sheets[0].cpbh;
								var Sql = string.Format(@" select Warehouse WarehouseName,s.number WarehouseID,s.AreaSort,t.Kcsl,unshipped from (
select Warehouse, SUM(sl) kcsl,
isnull((select SUM(sl) tempkcsl from scb_tempkcsl where nowdate = '{2}' and ck = Warehouse and cpbh = '{0}'), 0) tempkcsl,
isnull((select SUM(qty)  from tmp_pickinglist_occupy where cangku = Warehouse and cpbh = '{0}'), 0) unshipped
from scb_kcjl_location a inner join scb_kcjl_area_cangw b
on a.Warehouse = b.Location and a.Location = b.cangw
where b.state = 0 and a.Disable = 0  and b.cpbh = '{0}' and b.location in ('GB06','GB07') AND a.LocationType<10
group by Warehouse) t 
INNER JOIN dbo.scbck s ON s.id=t.Warehouse 
where kcsl - tempkcsl-unshipped  >= {1}", CurrentItemNo, Order.zsl, DateTime.Now.ToString("yyyy-MM-dd"));
								var WarehouseList = db.Database.SqlQuery<OrderMatchEntityGB>(Sql).ToList();
								Helper.Info(Order.number, "wms_api", "SFA:" + JsonConvert.SerializeObject(WarehouseList));
								if (WarehouseList.Any())
								{
									if (WarehouseList.Exists(p => p.WarehouseName == "GB06"))
									{
										OrderMatchResult.WarehouseID = 83;
										OrderMatchResult.WarehouseName = "GB06";
									}
									else
									{
										OrderMatchResult.WarehouseID = 213;
										OrderMatchResult.WarehouseName = "GB07";
									}
									OrderMatchResult.Result.State = 1;
									OrderMatchResult.Logicswayoid = 81;
									OrderMatchResult.SericeType = "SFA";
								}
								else
								{
									OrderMatchResult.Result.Message = string.Format("订单编号:{0}:SFA订单未匹配到仓库，请手动派单！", Order.number);
								}
								#endregion SFA
							}
						}
						else if (Order.temp3 == "7")  //退货单
						{
							if (Order.zsl > 1) //多包
							{
								OrderMatchResult.Result.Message = string.Format("订单编号:{0},多包裹请手动派单！", Order.number);
								return OrderMatchResult;
							}
							else
							{
								OrderMatchResult.Result.State = 1;
								OrderMatchResult.WarehouseID = 88;
								OrderMatchResult.WarehouseName = "GB98";
								OrderMatchResult.Logicswayoid = 30;
								OrderMatchResult.SericeType = "Parcelforce";

								//指定物流、仓库
								var carrier = db.scb_returnOrder_carrier.Where(p => p.orderId == Order.OrderID && p.nid == Order.number).FirstOrDefault();
								if (carrier != null)
								{
									if (carrier.warehouseid.HasValue && carrier.warehouseid > 0)
									{
										OrderMatchResult.WarehouseID = carrier.warehouseid.Value;
										OrderMatchResult.WarehouseName = db.scbck.First(p => p.number == OrderMatchResult.WarehouseID).id;
									}
									if (carrier.logicswayoid.HasValue && carrier.logicswayoid > 0)
									{
										OrderMatchResult.Logicswayoid = carrier.logicswayoid.Value;
										OrderMatchResult.SericeType = carrier.servicetype;
									}
								}
							}
						}

						else
						{
							//MatchWarehouseByGB_V2(Order, ref OrderMatchResult);
							MatchWarehouseByGB_V3(Order, ref OrderMatchResult);
							if (!string.IsNullOrEmpty(OrderMatchResult.WarehouseName))
							{
								//20220920开始使用XDP，所以取消这个代码
								//if (OrderMatchResult.Logicswayoid == 29) 
								//{
								//    OrderMatchResult.Logicswayoid = 20;
								//    OrderMatchResult.SericeType = "NEXTDAY";
								//}

								if (OrderMatchResult.Logicswayoid == 20)
								{
									if ((Order.country == "GB" && Order.temp10 != "GB") || Order.zip.StartsWith("BT") || Order.zip.StartsWith("IV"))
										OrderMatchResult.SericeType = "48HOURS";
								}
								//if (!string.IsNullOrEmpty(OrderMatchResult.Result.Message))
								//	return OrderMatchResult;
								if (string.IsNullOrEmpty(OrderMatchResult.SericeType) || OrderMatchResult.Logicswayoid == 0)
								{
									return OrderMatchResult;
								}


							}
							else
							{
								return OrderMatchResult;
							}

							if (!string.IsNullOrEmpty(OrderMatchResult.WarehouseName)
								&& !string.IsNullOrEmpty(OrderMatchResult.SericeType)
								&& OrderMatchResult.Logicswayoid > 0
								//&& string.IsNullOrEmpty(OrderMatchResult.Result.Message)
								)
								OrderMatchResult.Result.State = 1;
						}
					}
					else
					{
						OrderMatchResult.Result.Message = "订单不存在或者状态已变更，请重新查询！";
					}

				}
				else
					OrderMatchResult.Result.Message = "订单不存在或者状态已变更，请重新查询！";

			}
			catch (Exception ex)
			{
				OrderMatchResult.Result.Message = ex.Message;
				Helper.Error(number, "wms_api", ex);
			}
			return OrderMatchResult;
		}

		private OrderMatchResult BaseOrderByUSV3(MatchModel model, string datformat, bool isdev = false)
		{
			var OrderMatchResult = new OrderMatchResult()
			{
				SericeType = string.Empty,
				Result = new ResponseResult() { State = 0, Message = string.Empty }
			};
			if (!isdev && (model.state == 0 && (model.filterflag != 4 && model.filterflag != 5)))
			{
				OrderMatchResult.Result.Message = "订单不存在或者状态已变更，请重新查询！";
				return OrderMatchResult;
			}
			if (model.details.Any(p => string.IsNullOrEmpty(p.cpbh.Trim())))
			{
				OrderMatchResult.Result.Message = "订单存在空白货号！";
				return OrderMatchResult;
			}
			try
			{
				var db = new cxtradeModel();
				var MatchRules = db.scb_WMS_MatchRule.Where(f => f.State == 1).ToList();
				var _UPSSkuList = LoadUPSSKusIntoMemory();
				if (model.isReturn)
				{
					#region 退件
					if (model.servicetype == null)
						model.servicetype = "";
					var warehouse = db.scbck.FirstOrDefault(f => f.number == model.warehouseid);
					if (model.lywl.ToUpper() != "WFS" && warehouse == null)
					{
						//2022-08-24 和史倩文协商后，退件不判断订单的货号是否一定要相同
						// var xsjl = db.scb_xsjl.FirstOrDefault(f => f.OrderID == Order.OrderID && f.state == 0 && !string.IsNullOrEmpty(f.trackno) && (f.temp3 != "7" || f.temp3 != "12"));
						//2023-02-16和史倩文协商后，恢复相同货号的判断
						var sql = string.Format(@"select x.* from scb_xsjl x inner join scb_xsjlsheet s on x.number=s.father
                                                where x.OrderID = '{0}' and x.state = 0 and x.temp3 not in ('7','12') and s.cpbh = '{1}'
                                                and ISNULL(x.trackno, '') != ''",
											model.orderId, model.details.FirstOrDefault().cpbh);

						var xsjl = db.Database.SqlQuery<scb_xsjl>(sql).ToList().FirstOrDefault();
						if (xsjl == null)
						{
							try
							{
								var refundSql = $@" select x.* from refund r
			   left join scb_xsjlsheet s on s.number=r.bnumber
			   left join scb_xsjl x on x.number = s.father
			   where r.xsjlid = {model.number} and x.state = 0  and x.temp3 not in ('7','12') and ISNULL(x.trackno, '') != '' ";
								xsjl = db.Database.SqlQuery<scb_xsjl>(refundSql).ToList().FirstOrDefault();
							}
							catch { }

							if (xsjl == null)
							{
								OrderMatchResult.Result.Message = "没有原订单货号发货记录，请手动派单！";
								return OrderMatchResult;
							}
						}
						warehouse = db.scbck.FirstOrDefault(f => f.number == xsjl.warehouseid);
						if (model.servicetype.ToLower() != "fds_return")
						{
							model.logicswayoid = xsjl.logicswayoid;
							model.servicetype = xsjl.servicetype;
						}
						model.warehouseid = xsjl.warehouseid;

					}
					if (model.lywl.ToUpper() != "WFS" && (model.logicswayoid.HasValue || model.logicswayoid.Value == 0 || string.IsNullOrEmpty(model.servicetype) || model.logicswayoid.Value == 37))
					{
						var xsjl = db.scb_xsjl.FirstOrDefault(f => f.OrderID == model.orderId && f.state == 0 && !string.IsNullOrEmpty(f.trackno) && (f.temp3 != "7" || f.temp3 != "12"));
						if (xsjl == null)
						{
							OrderMatchResult.Result.Message = "没有原订单货号发货记录，请手动派单！";
							return OrderMatchResult;
						}
						else
						{
							if (xsjl.logicswayoid.Value == 37)
							{
								if (!xsjl.trackno.Contains("Z") && xsjl.trackno.Length == 12)
								{
									OrderMatchResult.Logicswayoid = 18;
									OrderMatchResult.SericeType = "FEDEX_GROUND";
								}
								else
								{
									OrderMatchResult.Logicswayoid = 4;
									OrderMatchResult.SericeType = "UPSGround";
								}
							}
							else
							{
								model.logicswayoid = xsjl.logicswayoid;
								model.servicetype = xsjl.servicetype;
								OrderMatchResult.Logicswayoid = model.logicswayoid.Value;
								OrderMatchResult.SericeType = model.servicetype;
							}
						}
					}
					else
					{
						OrderMatchResult.Logicswayoid = model.logicswayoid.Value;
						OrderMatchResult.SericeType = model.servicetype;
					}
					//2022.03.29 wjw要求 所有美国发美国的订单，退货label上面的地址全部变成us06的地址
					//if (warehouse.AreaLocation == "west" && Order.name != "wms")
					if (model.toCountry == "US" && model.name != "wms")
					{
						var zip = db.scb_ups_zip.Where(z => model.zip.StartsWith(z.DestZip)).FirstOrDefault();
						if (zip != null)
						{
							switch (zip.State)
							{
								case "MN":
								case "WI":
								case "IA":
								case "IL":
								case "MO":
								case "AR":
								case "LA":
								case "MI":
								case "IN":
								case "OH":
								case "KY":
								case "TN":
								case "MS":
								case "AL":
									OrderMatchResult.WarehouseID = 44;
									OrderMatchResult.WarehouseName = "US08";
									break;
								case "GA":
								case "SC":
								case "NC":
								case "FL":
								case "NY":
								case "PA":
								case "WV":
								case "VA":
								case "ME":
								case "VT":
								case "NH":
								case "MA":
								case "CT":
								case "RI":
								case "NJ":
								case "MD":
								case "DE":
									OrderMatchResult.WarehouseID = 82;
									OrderMatchResult.WarehouseName = "US11";
									break;
								case "WA":
								case "OR":
								case "ID":
								case "MT":
								case "ND":
								case "WY":
								case "SD":
								case "CA":
								case "NV":
								case "UT":
								case "CO":
								case "AZ":
								case "NM":
								case "NE":
								case "KS":
								case "OK":
								case "TX":
									OrderMatchResult.WarehouseID = 24;
									OrderMatchResult.WarehouseName = "US06";
									break;

								default:
									OrderMatchResult.WarehouseID = 24;
									OrderMatchResult.WarehouseName = "US06";
									break;
							}
						}
						else
						{
							OrderMatchResult.WarehouseID = 24;
							OrderMatchResult.WarehouseName = "US06";
						}

						#region 20250520 武金凤//指定平台店铺指定退货仓
						//20250520 武金凤 walmart 08和06的邮编去08，11的邮编还是11
						//20250522 武金凤 更新的退件地址
						//if (model.xspt.ToLower() == "walmart")
						//{
						//	if (OrderMatchResult.WarehouseName == "US06")
						//	{
						//		OrderMatchResult.WarehouseID = 44;
						//		OrderMatchResult.WarehouseName = "US08";
						//	}
						//	else if (OrderMatchResult.WarehouseName == "US11")
						//	{
						//		OrderMatchResult.WarehouseID = 82;
						//		OrderMatchResult.WarehouseName = "US11";
						//	}
						//}
						//20250520 武金凤 target都退到06
						if (model.xspt.ToLower() == "target")
						{
							OrderMatchResult.WarehouseID = 24;
							OrderMatchResult.WarehouseName = "US06";
						}
						//20250520 武金凤 homedepot都退到06
						else if (model.xspt.ToLower() == "homedepot")
						{
							OrderMatchResult.WarehouseID = 24;
							OrderMatchResult.WarehouseName = "US06";
						}
						//20250520 武金凤 am都退到06
						else if (model.xspt.ToLower() == "am" || model.xspt.ToLower() == "amazon")
						{
							OrderMatchResult.WarehouseID = 24;
							OrderMatchResult.WarehouseName = "US06";
						}
						#endregion

					}
					else if (model.toCountry == "CA" && model.name != "wms")
					{
						OrderMatchResult.WarehouseID = 141;
						OrderMatchResult.WarehouseName = "US21";
					}
					else
					{
						OrderMatchResult.WarehouseID = (int)model.warehouseid;
						OrderMatchResult.WarehouseName = warehouse.name;
					}

					if (model.lywl.ToUpper() == "WFS")
					{
						double weight = 0;
						double weight_g = 0;
						double? volNum = 0;
						double? volNum2 = 0;
						var sizes = new List<double?>();
						foreach (var sheet in model.details)
						{
							var good = db.N_goods.Where(g => g.cpbh == sheet.cpbh && g.country == model.country && g.groupflag == 0).FirstOrDefault();
							sizes.Add(good.bzgd);
							sizes.Add(good.bzcd);
							sizes.Add(good.bzkd);
							weight += (double)good.weight_express * sheet.sl / 1000 * 2.2046226;
							weight_g += (double)good.weight_express * sheet.sl;
							volNum += (good.bzcd + 2 * (good.bzkd + good.bzgd)) * sheet.sl;
							volNum2 += good.bzcd * good.bzgd * good.bzkd * sheet.sl;
							var sizesum = sizes.Sum(f => f) * 2 - sizes.Max();
						}
						double weight_vol = (double)volNum2 / 250;
						double weight_phy = weight_vol > weight ? weight_vol : weight;
						var maxNum = sizes.Max();
						var secNum = sizes.Where(f => f < maxNum).Max();
						if (sizes.Count(f => f < maxNum) <= sizes.Count - 2)
						{
							secNum = maxNum;
						}
						var configs = db.scb_WMS_LogisticsForWeight.Where(f => f.IsChecked == 1).ToList();
						var skModel = new ScbckModel()
						{
							id = OrderMatchResult.WarehouseName
						};
						WarehouseLevelV3(skModel, _UPSSkuList, configs, MatchRules, model, weight, weight_g, volNum, volNum2, weight_vol, weight_phy, maxNum, secNum, model.toCountry, false, ref OrderMatchResult, false);
					}
					else if (model.servicetype == "Prime" || model.logicswayoid == 37)
					{
						MatchLogisticsV3(model, ref OrderMatchResult);
					}
					else if (model.logicswayoid.Value == 79)
					{
						OrderMatchResult.Logicswayoid = 18;
						OrderMatchResult.SericeType = "FEDEX_GROUND";
					}
					else if (model.servicetype.Contains("Pioneer") || model.logicswayoid == 59)
					{
						if (model.servicetype.ToLower() != "fds_return")
						{
							OrderMatchResult.Logicswayoid = 18;
							OrderMatchResult.SericeType = "FEDEX_GROUND";
						}
						else
						{
							//自物流退货单优先最高
							OrderMatchResult.Logicswayoid = (int)model.logicswayoid;
							OrderMatchResult.SericeType = model.servicetype;
							OrderMatchResult.Result.State = 1;
							#region 墨西哥物流指定
							if (model.toCountry.ToUpper() == "MX")
							{
								OrderMatchResult.Logicswayoid = 48;
								OrderMatchResult.SericeType = "UPSInternational";
							}
							#endregion 墨西哥物流指定
							return OrderMatchResult;
						}
					}
					else
					{
						OrderMatchResult.Logicswayoid = (int)model.logicswayoid;
						OrderMatchResult.SericeType = model.servicetype;
					}
					//prime补充
					if (OrderMatchResult.Logicswayoid == 37)
					{
						if (OrderMatchResult.SericeType.ToLower().Contains("fedex"))
						{
							OrderMatchResult.Logicswayoid = 18;
							OrderMatchResult.SericeType = "FEDEX_GROUND";
						}
						else if (OrderMatchResult.SericeType.ToLower().Contains("upsground"))
						{
							OrderMatchResult.Logicswayoid = 4;
							OrderMatchResult.SericeType = "UPSGround";
						}

					}
					//fedex退件用标准服务
					if (OrderMatchResult.Logicswayoid == 18)
						OrderMatchResult.SericeType = "FEDEX_GROUND";

					//只用ups
					if (MatchRules.Any(f => f.Type == "Only_UPSGround_Send"))
					{
						var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_UPSGround_Send");
						var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
						if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == model.dianpu.ToUpper()))
						{
							OrderMatchResult.Logicswayoid = 4;
							OrderMatchResult.SericeType = "UPSGround";
						}
					}
					//只用fedex
					if (MatchRules.Any(f => f.Type == "Only_FedexGround_Send"))
					{
						var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_FedexGround_Send");
						var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
						if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == model.dianpu.ToUpper()))
						{
							OrderMatchResult.Logicswayoid = 18;
							OrderMatchResult.SericeType = "FEDEX_GROUND";
						}
					}

					//加拿大退到自己的仓库
					if (model.toCountry == "CA")
					{
						OrderMatchResult.Logicswayoid = 18;
						OrderMatchResult.SericeType = "FEDEX_GROUND";
						OrderMatchResult.WarehouseID = 141;
						OrderMatchResult.WarehouseName = "US21";
					}

					if (model.name == "wms")//第三方最后
					{
						if (OrderMatchResult.WarehouseName == "US08")
						{
							var zip = db.scb_ups_zip.FirstOrDefault(z => model.zip.StartsWith(z.DestZip) && z.Origin == "60446");
							if (zip != null)
							{
								if (zip.TNTDAYS > 3 && model.dianpu.ToLower() != "pioneer010".ToLower())
								{
									OrderMatchResult.SericeType = "Priority|Default|Parcel|OFF";
								}
							}
						}
						if (model.dianpu.ToLower() == "pioneer015".ToLower())
						{
							if (OrderMatchResult.SericeType == "UPSGround" || OrderMatchResult.Logicswayoid == 4)
							{
								OrderMatchResult.Logicswayoid = 18;
								OrderMatchResult.SericeType = "FEDEX_GROUND";
							}

						}
					}
					else
					{
						//同订单多个退件物流不同就用Fedex
						var return_orders = db.Database.SqlQuery<string>(string.Format(@"select distinct servicetype from scb_xsjl 
                        where OrderID = '{0}' and state = 0 and temp3 in ('7', '12') and filterflag<=11  and number != {1} ", model.orderId, model.number)).ToList();
						if (return_orders.Count > 1)
						{
							OrderMatchResult.Logicswayoid = 18;
							OrderMatchResult.SericeType = "FEDEX_GROUND";
						}

						var return_Fedex_orders = db.Database.SqlQuery<string>(string.Format(@"select servicetype from scb_xsjl 
                        where OrderID = '{0}' and state = 0 and temp3 in ('7', '12') and filterflag<=11
                        and number != {1} and logicswayoid=18", model.orderId, model.number)).ToList();
						if (return_Fedex_orders.Count > 0)
						{
							OrderMatchResult.Logicswayoid = 18;
							OrderMatchResult.SericeType = "FEDEX_GROUND";
						}
					}

					//20231013 史倩文  所有return label的物流麻烦都调整成fedex
					OrderMatchResult.Logicswayoid = 18;
					OrderMatchResult.SericeType = "FEDEX_GROUND";

					//20250417 史倩文 加拿大本地的退货单，默认用ups、
					if (model.toCountry == "CA")
					{
						OrderMatchResult.Logicswayoid = 4;
						OrderMatchResult.SericeType = "UPSGround";
					}

					//指定物流、仓库
					var nid = decimal.Parse(model.number);
					var carrier = db.scb_returnOrder_carrier.Where(p => p.orderId == model.orderId && p.nid == nid).FirstOrDefault();
					if (carrier != null)
					{
						if (carrier.warehouseid.HasValue && carrier.warehouseid > 0)
						{
							OrderMatchResult.WarehouseID = carrier.warehouseid.Value;
							OrderMatchResult.WarehouseName = db.scbck.First(p => p.number == OrderMatchResult.WarehouseID).id;
						}
						if (carrier.logicswayoid.HasValue && carrier.logicswayoid > 0)
						{
							OrderMatchResult.Logicswayoid = carrier.logicswayoid.Value;
							OrderMatchResult.SericeType = carrier.servicetype;
							if (OrderMatchResult.SericeType == "UPS" && carrier.logicswayoid == 4)
							{
								OrderMatchResult.SericeType = "UPSGround";
							}
						}
					}

					OrderMatchResult.Result.State = 1;

					#endregion
				}
				else
				{
					#region 发货
					foreach (var item in model.details)
					{
						if (item.cpbh.ToLower().Equals("seel-bp17"))
						{
							string upcMsg = "SEEL-BP17请直接转至全部订单！";
							Helper.Info(model.number, "wms_api", upcMsg);
							OrderMatchResult.Result.Message = upcMsg;
							return OrderMatchResult;

						}
					}

					#region  costway 店铺 FP10348US-WH使用Spreetail
					if (model.dianpu.ToLower() == "costway" && model.details.Any(a => a.cpbh == "FP10348US-WH") && model.temp3 == "0")
					{
						OrderMatchResult.Logicswayoid = 138;
						OrderMatchResult.SericeType = "Spreetail";
						OrderMatchResult.Result.State = 1;
						OrderMatchResult.WarehouseName = "";
						return OrderMatchResult;
					}
					#endregion

					#region  Target-topbuy 店铺
					if (model.dianpu.ToUpper() == "TARGET-TOPBUY")
					{
						string upc_code = "";
						foreach (var item in model.details)
						{
							var ca_sku_upc = db.ca_Sku_Upc.Where(d => d.Sku == item.sku && d.StoreName.ToUpper() == "TARGET-TOPBUY" && d.Upc != "").FirstOrDefault();
							if (ca_sku_upc == null)
							{
								string upcMsg = "Target-topbuy店铺订单sku在Ca_Sku_Upc查询失败，无法匹配！请先在“快乐星球”系统里做sku与upc的对应关系！";
								Helper.Info(model.number, "wms_api", upcMsg);
								OrderMatchResult.Result.Message = upcMsg;
								return OrderMatchResult;
							}
							var scb_target_tcin = db.scb_target_tcin.Where(p => p.PartnerSKU == item.sku && p.shop.ToUpper() == "TARGET-TOPBUY" && p.TCIN != "").FirstOrDefault();
							if (scb_target_tcin == null)
							{
								string tcinMsg = "Target-topbuy店铺订单sku在scb_target_tcin查询失败，无法匹配！请先在“快乐星球”系统里做sku与tcin的对应关系！";
								Helper.Info(model.number, "wms_api", tcinMsg);
								OrderMatchResult.Result.Message = tcinMsg;
								return OrderMatchResult;
							}
						}
					}
					#endregion

					#region  Target店铺
					if (model.dianpu.ToUpper() == "TARGET")
					{
						string upc_code = "";
						foreach (var item in model.details)
						{
							var ca_sku_upc = db.ca_Sku_Upc.Where(d => d.Sku == item.sku && d.StoreName.ToUpper() == "TARGET" && d.Upc != "").FirstOrDefault();
							if (ca_sku_upc == null)
							{
								string upcMsg = "Target店铺订单sku在Ca_Sku_Upc查询失败，无法匹配！请先在“快乐星球”系统里做sku与upc的对应关系！";
								Helper.Info(model.number, "wms_api", upcMsg);
								OrderMatchResult.Result.Message = upcMsg;
								return OrderMatchResult;
							}
							var scb_target_tcin = db.scb_target_tcin.Where(p => p.PartnerSKU == item.sku && p.shop.ToUpper() == "TARGET" && p.TCIN != "").FirstOrDefault();
							if (scb_target_tcin == null)
							{
								string tcinMsg = "Target店铺订单sku在scb_target_tcin查询失败，无法匹配！请先在“快乐星球”系统里做sku与tcin的对应关系！";
								Helper.Info(model.number, "wms_api", tcinMsg);
								OrderMatchResult.Result.Message = tcinMsg;
								return OrderMatchResult;
							}
						}
					}
					#endregion

					#region homedepot
					if (model.dianpu.ToUpper() == "HOMEDEPOT")
					{
						string upc_code = "";
						foreach (var item in model.details)
						{
							var ca_sku_upc = db.ca_Sku_Upc.Where(d => d.Sku == item.sku && d.StoreName.ToUpper() == model.dianpu.ToUpper() && d.Upc != "").FirstOrDefault();
							if (ca_sku_upc == null || string.IsNullOrEmpty(ca_sku_upc.Upc))
							{
								string upcMsg = $"{model.dianpu}店铺订单sku在Ca_Sku_Upc查询失败，无法匹配！请先在“快乐星球”系统里做sku与upc的对应关系！";
								Helper.Info(model.number, "wms_api", upcMsg);
								OrderMatchResult.Result.Message = upcMsg;
								return OrderMatchResult;
							}
						}
					}
					#endregion

					#region HD-gymax
					if (model.dianpu.ToUpper() == "HD-GYMAX")
					{
						string upc_code = "";
						foreach (var item in model.details)
						{
							var ca_sku_upc = db.ca_Sku_Upc.Where(d => d.Sku == item.sku && d.StoreName.ToUpper() == model.dianpu.ToUpper() && d.Upc != "").FirstOrDefault();
							if (ca_sku_upc == null || string.IsNullOrEmpty(ca_sku_upc.Upc))
							{
								string upcMsg = $"{model.dianpu}店铺订单sku在Ca_Sku_Upc查询失败，无法匹配！请先在“快乐星球”系统里做sku与upc的对应关系！";
								Helper.Info(model.number, "wms_api", upcMsg);
								OrderMatchResult.Result.Message = upcMsg;
								return OrderMatchResult;
							}
						}
					}
					#endregion



					var isprime = model.dianpu.ToLower().EndsWith("prime");

					MatchLogisticsByUSV3(model, _UPSSkuList, datformat, isprime, ref OrderMatchResult);


					if (!string.IsNullOrEmpty(OrderMatchResult.WarehouseName) && OrderMatchResult.SericeType == "LTL")
					{
						OrderMatchResult.Result.Message = $"oversize 订单，LTL 运费：{OrderMatchResult.MatchFee}, 建议卡车,仓库：{OrderMatchResult.WarehouseName}";
						OrderMatchResult.WarehouseName = "";
						OrderMatchResult.SericeType = "";
						return OrderMatchResult;
					}


					//20240507 US仓库不用zpl
					//var CanZpl = CanSendByZPL(Order.number, OrderSheets.FirstOrDefault().cpbh, OrderMatchResult.WarehouseName, OrderMatchResult.Logicswayoid);
					OrderMatchResult.CanZPL = false;//CanZpl;
					if (string.IsNullOrEmpty(OrderMatchResult.WarehouseName))
					{
						#region 墨西哥物流指定
						if (model.toCountry.ToUpper() == "MX")
						{
							OrderMatchResult.Logicswayoid = 48;
							OrderMatchResult.SericeType = "UPSInternational";
							return OrderMatchResult;
						}
						#endregion 墨西哥物流指定

					}
					//2023-01-29 target不发surepost，改发UPS或FedEx
					if (model.xspt.ToLower() == "target" && OrderMatchResult.Logicswayoid == 24)
					{
						OrderMatchResult.Logicswayoid = 4;
						OrderMatchResult.SericeType = "UPSGround";
					}
					//2023-10-13 walmart想先不发surepost 改发FedEx
					if (model.dianpu.ToLower() == "walmart" && OrderMatchResult.Logicswayoid == 24)
					{
						OrderMatchResult.Logicswayoid = 18;
						OrderMatchResult.SericeType = "FEDEX_GROUND";
					}

					#region 三方特殊匹配
					else if (model.name == "wms")//第三方最后
					{
						if (OrderMatchResult.WarehouseName == "US08")
						{
							var zip = db.scb_ups_zip.FirstOrDefault(z => model.zip.StartsWith(z.DestZip) && z.Origin == "60446");
							if (zip != null)
							{
								if (zip.TNTDAYS > 3 && model.dianpu.ToLower() != "pioneer010".ToLower())
								{
									OrderMatchResult.SericeType = "Priority|Default|Parcel|OFF";
								}
							}
						}
					}
					#endregion

					#region CA BC只发Fedex

					else if (OrderMatchResult.WarehouseName == "US121" && OrderMatchResult.SericeType == "UPSInternational"
					   && (model.statsa == "BC" || model.statsa == "British Columbia"))
					{
						OrderMatchResult.Logicswayoid = 18;
						OrderMatchResult.SericeType = "FEDEX_GROUND";
					}
					else if (OrderMatchResult.WarehouseName == "US21" && OrderMatchResult.SericeType == "UPSInternational"
						&& (model.statsa == "BC" || model.statsa == "British Columbia"))
					{
						OrderMatchResult.Logicswayoid = 18;
						OrderMatchResult.SericeType = "FEDEX_GROUND";
					}

					#endregion

					#region CA Wayfair调整物流名字
					if (model.xspt.ToLower() == "wayfair" && model.toCountry.ToUpper() == "CA" && (OrderMatchResult.WarehouseName == "US17" || OrderMatchResult.WarehouseName == "US16" || OrderMatchResult.WarehouseName == "US06" || OrderMatchResult.WarehouseName == "US11"))
					{
						OrderMatchResult.SericeType = "UPSInternational";
					}
					#endregion


					#region CA 物流规则
					#region CA UPSInternational 改成UPS
					if ((OrderMatchResult.WarehouseName == "US121" || OrderMatchResult.WarehouseName == "US21") && OrderMatchResult.SericeType == "UPSInternational")
					{
						var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_FedexGround_Send");
						var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
						if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == model.dianpu.ToUpper()))
						{
							OrderMatchResult.Logicswayoid = 18;
							OrderMatchResult.SericeType = "FEDEX_GROUND";
						}
						else
						{
							OrderMatchResult.Logicswayoid = 4;
							OrderMatchResult.SericeType = "UPSGround";
						}
					}
					#endregion

					#region CA UPSInternational  
					//20241216 张洁楠 从现在新的订单开始US08全部改成用UPStoCA发货；
					//20241230 张洁楠 08变了 08不发upstoca也不发FedExtoca，只发ups international
					//20250113 张洁楠 Tacoma仓库的加拿大订单统一发FedExtoCA
					if ((OrderMatchResult.WarehouseName == "US17") && OrderMatchResult.SericeType == "UPSInternational" && model.toCountry.ToUpper() == "CA")
					{
						OrderMatchResult.Logicswayoid = 78;
						OrderMatchResult.SericeType = "FedexToCA";
					}

					////20241210 张洁楠 US08主仓及配件仓全部用FedExtoCA发货，并且用#675084190账号打单；
					//if (OrderMatchResult.WarehouseName == "US08" && OrderMatchResult.SericeType == "UPSInternational" && model.toCountry.ToUpper() == "CA")
					//{
					//	OrderMatchResult.Logicswayoid = 78;
					//	OrderMatchResult.SericeType = "FedexToCA";
					//}

					//20240416 张洁楠 从4.17开始，对于从US06/US16发出的订单，Giantex-CA店铺+DORTALA-CA店铺这两个店铺的订单改发UPStoCA
					//20240424 张洁楠 Giantex-CA和KOTEKUS-CA 这两个店铺的订单发FedExtoCA
					//20241210 张洁楠 只有wayfair平台全部店铺需要用FedExtoCA发货
					//20250113 张洁楠 Redlands+Fontana、US11 加拿大订单统一发UPStoCA
					//20251021 张洁楠 US15 加拿大发UPStoCA
					if ((OrderMatchResult.WarehouseName == "US15" || OrderMatchResult.WarehouseName == "US16" || OrderMatchResult.WarehouseName == "US06" || OrderMatchResult.WarehouseName == "US11") && OrderMatchResult.SericeType == "UPSInternational" && model.toCountry.ToUpper() == "CA")
					{
						#region   20250109  张洁楠 Redlands和Fontana从现在开始的新单子，需要全部发FedExtoCA
						//            var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_FedexGround_Send");
						//            var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();

						//            if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == model.dianpu.ToUpper())
						//                 || model.xspt.ToLower().Equals("wayfair")
						//            //|| model.xspt.ToLower().Equals("walmart") || model.xspt.ToLower().Equals("wayfair") || model.dianpu.ToLower().Equals("tangkula-ca")
						//            //|| model.dianpu.ToLower().Equals("giantex-ca") || model.dianpu.ToUpper().Equals("KOTEKUS-CA") || model.details.Any(p => p.cpbh == "HV10064")
						//            )
						//            {
						//                OrderMatchResult.Logicswayoid = 78;
						//                OrderMatchResult.SericeType = "FedexToCA";
						//            }
						//            else
						//            {
						//                //var realsize = cpRealSizeinfos.First();
						//                //decimal girth = realsize.bzcd + 2 * (realsize.bzkd + realsize.bzgd);
						//                var cpbh = model.details.First().cpbh;
						//                var CpbhSizeInfo = db.scb_realsize.Where(p => p.cpbh == cpbh && p.country == model.country)
						//                    .Select(p => new { cpbh = p.cpbh, bzcd = p.bzcd, bzkd = p.bzkd, bzgd = p.bzgd }).FirstOrDefault();

						//                if (CpbhSizeInfo == null)
						//                {
						//                    CpbhSizeInfo = db.N_goods.Where(p => p.cpbh == cpbh && p.country == model.country)
						//                        .Select(p => new { cpbh = p.cpbh, bzcd = p.bzcd, bzkd = p.bzkd, bzgd = p.bzgd }).FirstOrDefault();
						//                }
						//                var girth = CpbhSizeInfo.bzcd + 2 * (CpbhSizeInfo.bzkd + CpbhSizeInfo.bzgd);

						//                //实际尺寸的体积重
						//                var real_volNum2 = CpbhSizeInfo.bzcd * CpbhSizeInfo.bzkd * CpbhSizeInfo.bzgd / 320;
						//                if (girth > 130 || CpbhSizeInfo.bzcd > 96 || real_volNum2 > 63)
						//                {
						//                    OrderMatchResult.Logicswayoid = 78;
						//                    OrderMatchResult.SericeType = "FedexToCA";
						//                }
						//                else
						//                {
						//                    var dattemp = DateTime.Now;
						//                    var startTime = dattemp;
						//                    var endTime = dattemp.AddDays(1);

						//                    var qty1 = db.Database.SqlQuery<int>(string.Format($@"select isnull(SUM(x.zsl),0) from scb_xsjl x 
						//            where country = 'US' and x.temp10 = 'CA'
						//            and x.state = 0 and filterflag > 5 AND x.newdateis>'{startTime.ToString("yyyy-MM-dd")}' AND x.newdateis<'{endTime.ToString("yyyy-MM-dd")}'
						//AND x.hthm NOT LIKE 'Order%' AND x.hthm NOT LIKE 'return%'AND x.logicswayoid in (78) and x.warehouseid in (24,99)")).ToList().FirstOrDefault();
						//                    if (qty1 <= 220)
						//                    {
						//                        OrderMatchResult.Logicswayoid = 78;
						//                        OrderMatchResult.SericeType = "FedexToCA";
						//                    }
						//                    else  //fedexToCA限量220单，超过220单就用UPStoCA
						//                    {
						//                        OrderMatchResult.Logicswayoid = 80;
						//                        OrderMatchResult.SericeType = "UPSToCA";
						//                    }
						//                }
						//            }

						#endregion

						OrderMatchResult.Logicswayoid = 80;
						OrderMatchResult.SericeType = "UPSToCA";
					}


					//20241127 张洁楠 YT、NT、NU这三个地方需要匹配FedExtoCA，06，16没货都发UPSinternational国际件
					var CANoUPS = MatchRules.FirstOrDefault(f => f.Type == "CANoUPS");
					if (CANoUPS != null)
					{
						var CANoUPSs = db.scb_WMS_MatchRule_Details.Where(f => f.RID == CANoUPS.ID && f.Type == "ZIP").ToList(); //db.scb_WMS_MatchRule_Details.Where(f => f.RID == CANoZip.ID && f.Type == "ZIP").ToList();
						if (CANoUPSs.Where(p => model.zip.StartsWith(p.Parameter)).Any() && model.toCountry.ToUpper() == "CA")
						{
							if (model.xspt.ToLower() == "vendor" && OrderMatchResult.SericeType.Contains("UPS"))
							{
								OrderMatchResult.Logicswayoid = 0;
								OrderMatchResult.SericeType = "";
								OrderMatchResult.Result.Message = string.Format("vendor-ca偏远地区发不了，联系运营取消");
								return OrderMatchResult;
							}

							if (OrderMatchResult.WarehouseName == "US17")
							{
								OrderMatchResult.Logicswayoid = 78;
								OrderMatchResult.SericeType = "FedexToCA";
							}
							//加拿大本地QQ仓也配置下吧，如果目的地是YT、NT、NU只能用FedEx发货
							else if (OrderMatchResult.WarehouseName == "US21" || OrderMatchResult.WarehouseName == "US121")
							{
								OrderMatchResult.Logicswayoid = 18;
								OrderMatchResult.SericeType = "FEDEX_GROUND";
							}
							else
							{
								OrderMatchResult.Logicswayoid = 48;
								OrderMatchResult.SericeType = "UPSInternational";
							}
						}
					}
					//20250107 张洁楠 SP38244US 这个货号之后如果是发往加拿大的，指定用FedEx发货，如果是本地仓用FedEx，如果是中转仓用FedExtoCA
					//20250314 张洁楠 SP38245US 这个货号如果是加拿大本地仓或者美国中转仓发的话，指定用FedEx或者FedExtoCA
					if (model.details.Any(p => p.cpbh == "SP38244US" || p.cpbh == "SP38245US") && model.toCountry.ToUpper() == "CA")
					{
						if (OrderMatchResult.WarehouseName == "US21" || OrderMatchResult.WarehouseName == "US121")
						{
							OrderMatchResult.Logicswayoid = 18;
							OrderMatchResult.SericeType = "FEDEX_GROUND";
						}
						else
						{
							OrderMatchResult.Logicswayoid = 78;
							OrderMatchResult.SericeType = "FedexToCA";
						}
					}
					#endregion
					#endregion

					#region US21特殊规则
					//20240527 张洁楠 这个只调整这一周的 下周开始正常
					var dat = DateTime.Now;
					if (model.toCountry == "CA" && dat < new DateTime(2024, 5, 30, 0, 0, 0, 0) && OrderMatchResult.WarehouseName == "US21" && OrderMatchResult.SericeType.Contains("UPS") && "0,1,6".Contains(dat.DayOfWeek.ToString("d")))
					{
						try
						{
							var startTime = dat;
							var endTime = dat;
							if (dat.DayOfWeek == DayOfWeek.Saturday)
							{
								startTime = dat.Date;
								endTime = dat.Date.AddDays(3);
							}
							else if (dat.DayOfWeek == DayOfWeek.Sunday)
							{
								startTime = dat.Date.AddDays(-1);
								endTime = dat.Date.AddDays(2);
							}
							else if (dat.DayOfWeek == DayOfWeek.Monday)
							{
								startTime = dat.Date.AddDays(-2);
								endTime = dat.Date.AddDays(1);
							}
							var qty1 = db.Database.SqlQuery<int>(string.Format($@"select isnull(SUM(x.zsl),0) from scb_xsjl(nolock) x 
								                where country = 'US' and x.temp10 = 'CA'
								                and x.state = 0 and filterflag > 5 AND x.newdateis>'{startTime.ToString("yyyy-MM-dd")}' AND x.newdateis<'{endTime.ToString("yyyy-MM-dd")}'
												AND x.hthm NOT LIKE 'Order%' AND x.hthm NOT LIKE 'return%'AND x.logicswayoid in (4) and x.warehouseid in (141)")).ToList().FirstOrDefault();
							if (qty1 > 1000)
							{
								OrderMatchResult.Logicswayoid = 18;
								OrderMatchResult.SericeType = "FEDEX_GROUND";
							}
						}
						catch (Exception ex)
						{
							OrderMatchResult.Logicswayoid = 18;
							OrderMatchResult.SericeType = "FEDEX_GROUND";
						}
					}
					#endregion

					if ((model.dianpu.ToLower() == "arnot" || model.dianpu.ToLower() == "wellhut") && OrderMatchResult.Logicswayoid == 24)
					{
						OrderMatchResult.Logicswayoid = 4;
						OrderMatchResult.SericeType = "UPSGround";
					}
					//刘志明店铺，只用Fedex
					if (model.dianpu == "Costway-PhilipCA")
					{
						if (OrderMatchResult.Logicswayoid == 80)
						{
							OrderMatchResult.Logicswayoid = 78;
							OrderMatchResult.SericeType = "FedexToCA";
						}
						if (OrderMatchResult.Logicswayoid == 4)
						{
							OrderMatchResult.Logicswayoid = 18;
							OrderMatchResult.SericeType = "FEDEX_GROUND";
						}

					}
					if (model.dianpu.ToLower() == "overstock-ca")
					{
						if (OrderMatchResult.Logicswayoid == 78)
						{
							OrderMatchResult.Logicswayoid = 80;
							OrderMatchResult.SericeType = "UPSToCA";
						}
						if (OrderMatchResult.Logicswayoid == 18)
						{
							OrderMatchResult.Logicswayoid = 4;
							OrderMatchResult.SericeType = "UPSGround";
						}
					}
					#region 墨西哥物流指定
					if (model.toCountry.ToUpper() == "MX")
					{
						OrderMatchResult.Logicswayoid = 48;
						OrderMatchResult.SericeType = "UPSInternational";
					}
					#endregion 墨西哥物流指定

					if (model.toCountry == "CA")//FedEx
					{
						int[] fedex_oidList = new int[] { 18, 58, 78, 79 };
						int[] ups_oidList = new int[] { 4, 44, 47, 75, 80 };//UPSInternational除外
						if (fedex_oidList.Contains(OrderMatchResult.Logicswayoid))
						{
							var MatchRuleCA = MatchRules.FirstOrDefault(f => f.Type == "Remote_DistrictsByCA");
							var MatchRule_DetailsCA = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRuleCA.ID && f.Type == "Zip").ToList();
							if (MatchRule_DetailsCA.Any(f => model.zip.Replace(" ", "").StartsWith(f.Parameter)) &&
								(MatchRuleCA.Shop.ToUpper().Split(',').Contains(model.dianpu.ToUpper()) || model.xspt.ToLower().Equals("temu")))
							{
								OrderMatchResult.Result.Message = string.Format("匹配错误,加拿大特殊邮编[{0}]禁止FedEx发货！", model.zip);
								return OrderMatchResult;
							}
						}
						else if (ups_oidList.Contains(OrderMatchResult.Logicswayoid))
						{
							var MatchRuleCA = MatchRules.FirstOrDefault(f => f.Type == "Remote_DistrictsByCA");
							var tempzip = model.zip.Replace(" ", "");
							string sqlAccord = $"SELECT * FROM [dbo].[scb_WMS_MatchRule_Details_CA_UPS] WHERE '{tempzip}'>=PostalCodeFrom AND '{tempzip}'<=PostalCodeTo";
							var listAccord = db.Database.SqlQuery<scb_WMS_MatchRule_Details_CA_UPS>(sqlAccord).ToList();
							if (listAccord.Any() &&
								(MatchRuleCA.Shop.ToUpper().Split(',').Contains(model.dianpu.ToUpper()) || model.xspt.ToLower().Equals("temu")))
							{
								OrderMatchResult.Result.Message = string.Format("匹配错误,加拿大特殊邮编[{0}]禁止UPS发货！ ", model.zip);
								return OrderMatchResult;
							}
						}
					}

					#region  US21、US121 CA发US，禁用UPS
					//OrderMatch.WarehouseName == "US21" || OrderMatch.WarehouseName == "US121") && Tocountry == "US"
					if ((OrderMatchResult.WarehouseName == "US21" || OrderMatchResult.WarehouseName == "US121") && model.toCountry == "US" && OrderMatchResult.SericeType.Contains("UPS"))
					{
						OrderMatchResult.Result.Message = "CA发US，禁用UPS！";
						OrderMatchResult.WarehouseName = "";
						OrderMatchResult.SericeType = "";
					}
					#endregion

					//20231103 wjf US10禁用UPS
					if (OrderMatchResult.WarehouseName == "US10" && OrderMatchResult.SericeType.Contains("UPS"))
					{
						if (db.scb_order_carrier.Any(p => p.OrderID == model.orderId && p.dianpu == model.dianpu && p.oid.HasValue) || model.toCountry != "US")
						{
							OrderMatchResult.Result.Message = "US10禁用UPS！";
							OrderMatchResult.WarehouseName = "";
							OrderMatchResult.SericeType = "";
						}
						else
						{
							OrderMatchResult.Logicswayoid = 18;
							OrderMatchResult.SericeType = "FEDEX_GROUND";
						}
					}
					if ((OrderMatchResult.Logicswayoid == 4 || OrderMatchResult.Logicswayoid == 48) && (OrderMatchResult.WarehouseName == "US17" || OrderMatchResult.WarehouseName == "US04" || OrderMatchResult.WarehouseName == "US78" || OrderMatchResult.WarehouseName == "US136"))
					{
						OrderMatchResult.WarehouseName = "";
						OrderMatchResult.SericeType = "";
						OrderMatchResult.Result.Message = string.Format("US17,US04 禁止UPS,UPS international发货！ ");
						return OrderMatchResult;
					}

					if (!string.IsNullOrEmpty(OrderMatchResult.WarehouseName) && !string.IsNullOrEmpty(OrderMatchResult.SericeType))
					{
						OrderMatchResult.Result.State = 1;
						var fee = OrderMatchResult.MatchFee ?? 0;
						if (fee > 0 && fee < 999)
						{

						}
						else
						{
							OrderMatchResult.MatchFee = null;
						}
					}



					#endregion
				}

			}
			catch (Exception ex)
			{
				OrderMatchResult.Result.Message = ex.Message;
				LogHelper.WriteLog($"{System.Reflection.MethodBase.GetCurrentMethod().Name} request:[{JsonConvert.SerializeObject(model)}] ,responds:[{JsonConvert.SerializeObject(OrderMatchResult)}],ex:{JsonConvert.SerializeObject(ex)}");
			}
			return OrderMatchResult;
		}


		#endregion

		#region 物流匹配

		/// <summary>
		/// 美国匹配第二版
		/// </summary>
		/// <param name="Order">订单信息</param>
		/// <param name="OrderMatch">匹配结果</param>
		private void MatchLogistics2(scb_xsjl Order, ref OrderMatchResult OrderMatch)
		{
			var db = new cxtradeModel();
			var sheets = db.scb_xsjlsheet.Where(f => f.father == Order.number).ToList();
			var sheet = sheets.FirstOrDefault();
			var good = db.N_goods.Where(g => g.cpbh == sheet.cpbh && g.country == Order.country && g.groupflag == 0).FirstOrDefault();
			var entity = OrderMatch;
			var warehouse = db.scbck.Find(entity.WarehouseID);
			var current = new scb_LogisticsForWeight();

			double weight = 0;
			var sizes = new List<double?>();
			foreach (var sheet2 in sheets)
			{
				var good2 = db.N_goods.Where(g => g.cpbh == sheet.cpbh && g.country == Order.country && g.groupflag == 0).FirstOrDefault();
				weight += (double)good.weight_express * sheet2.sl;
			}


			var country = Order.country;
			if (weight > 0)
			{
				if (country == "US")
				{
					int ic = int.Parse(Math.Round((double)weight, MidpointRounding.AwayFromZero).ToString());
					var dtc2 = db.scb_LogisticsForWeight.Where(w => w.IsChecked == 1 && w.Warehouse == warehouse.realname).ToList();
					var dtc = new List<scb_LogisticsForWeight>();
					foreach (var w in dtc2)
					{
						if (int.Parse(w.Max) >= ic && int.Parse(w.Min) <= ic)
						{
							dtc.Add(w);
						}
					}
					if (dtc.Any())
					{
						double bzcd = (double)good.bzcd;
						double bzgd = (double)good.bzgd;
						double bzkd = (double)good.bzkd;
						if (bzcd != 0 && bzgd != 0 && bzkd != 0)
						{
							double maxNum = Math.Max(Math.Max(bzcd, bzkd), bzgd);//最大值
							double[] strSecond = new double[3] { bzcd, bzkd, bzgd };

							double maxValue, secondValue;
							GetMaxMiddValue(strSecond, out maxValue, out secondValue);

							bool flag = false;//判断标识
							foreach (var item in dtc)
							{
								if (item.LogisticsServiceMode == "UPSSurePost")
								{
									if (secondValue <= double.Parse(item.Kuan) && maxNum <= double.Parse(item.Chang) && (bzcd * bzkd * bzgd) <= (double)item.Size && (bzcd * bzkd * bzgd) <= 2900)
									{
										current = item;
									}
									else
									{
										current.LogisticsID = 4;
										current.LogisticsServiceMode = "UPSGround";
									}
									flag = true;
									break;
								}
							}
							if (!flag)
							{
								current = dtc.FirstOrDefault();
							}
							OrderMatch.Logicswayoid = current.LogisticsID;
							if (current.LogisticsID == 3)
								OrderMatch.SericeType = "First|Default|" + current.LogisticsServiceMode + "|OFF";
							else
								OrderMatch.SericeType = current.LogisticsServiceMode;
							return;
						}
					}

				}

			}
		}

		/// <summary>
		/// 美国匹配第三版
		/// </summary>
		/// <param name="Order">订单信息</param>
		/// <param name="OrderMatch">匹配结果</param>
		private void MatchLogistics3(scb_xsjl Order, ref OrderMatchResult OrderMatch)
		{
			var db = new cxtradeModel();
			var country = Order.country;
			var Tocountry = Order.temp10;
			var dianpuTrue = Order.dianpu;

			#region 固定/指定物流
			//国际件
			if (country == "US" && (Tocountry == "CA" || Tocountry == "MX") && !dianpuTrue.ToLower().Contains("grouponvendor-ca"))
			{
				OrderMatch.Logicswayoid = 48;
				OrderMatch.SericeType = "UPSInternational";
				return;
			}

			//指定物流
			var IsFind = true;
			//if (dianpuTrue.ToLower() == "walmartvendor")
			//    IsFind = true;
			//if ("Dawndior,GOVendor,GXVendor,GOVendor-W,GXVendor-W,COSTWAY-GZLOWES".Split(',').Contains(dianpuTrue))
			//    IsFind = true;
			//if (dianpuTrue.ToLower().Contains("wayfair") && country == "US")
			//    IsFind = true;
			if (IsFind)
			{
				//var order_carrier2 = db.scb_order_carrier.Where(c => c.OrderID == Order.OrderID && c.dianpu == Order.dianpu && c.oid != null);
				var order_carrier2 = db.Database.SqlQuery<scb_order_carrier>(string.Format("select * from scb_order_carrier where orderid='{0}' and dianpu='{1}' and oid is not null"
					, Order.OrderID, Order.dianpu)).ToList();
				if (order_carrier2.Any())
				{
					var order_carrier = order_carrier2.First();
					OrderMatch.Logicswayoid = (int)order_carrier.oid;
					if (string.IsNullOrEmpty(order_carrier.ShipService))
					{
						if (OrderMatch.Logicswayoid == 4)
							order_carrier.ShipService = "UPSGround";
						else if (OrderMatch.Logicswayoid == 18)
							order_carrier.ShipService = "FEDEX_GROUND";

					}
					OrderMatch.SericeType = order_carrier.ShipService;

					if (OrderMatch.SericeType == "Transportation-wayfair")
					{
						int WarehouseID = int.Parse(order_carrier.temp);
						OrderMatch.WarehouseID = WarehouseID;
						OrderMatch.WarehouseName = db.scbck.FirstOrDefault(f => f.number == WarehouseID).id;
					}
					return;
				}
			}
			//if (dianpuTrue.ToLower().Contains("wayfair") && country == "US")
			//{
			//    OrderMatch.Logicswayoid = 18;
			//    OrderMatch.SericeType = "FEDEX_GROUND";
			//    return;
			//}
			//if (dianpuTrue.ToLower() == "macys" && country == "US")
			//{
			//    OrderMatch.Logicswayoid = 4;
			//    OrderMatch.SericeType = "UPSGround";
			//    return;
			//}
			#endregion

			#region 初始化重量尺寸等
			var disable = db.scb_Logisticsmode.Where(f => f.IsEnable == 0).ToList();

			var sheets = db.scb_xsjlsheet.Where(f => f.father == Order.number).ToList();
			double weight = 0;
			double weight_g = 0;
			double? volNum = 0;
			double? volNum2 = 0;
			var sizes = new List<double?>();
			foreach (var sheet in sheets)
			{
				var good = db.N_goods.Where(g => g.cpbh == sheet.cpbh && g.country == country && g.groupflag == 0).FirstOrDefault();
				sizes.Add(good.bzgd);
				sizes.Add(good.bzcd);
				sizes.Add(good.bzkd);
				weight += (double)good.weight_express * sheet.sl / 1000 * 2.2046226;
				weight_g += (double)good.weight_express * sheet.sl;
				volNum += (good.bzcd + 2 * (good.bzkd + good.bzgd)) * sheet.sl;
				volNum2 += good.bzcd * good.bzgd * good.bzkd * sheet.sl;
			}
			double weight_vol = (double)volNum2 / 250;
			double weight_phy = weight_vol > weight ? weight_vol : weight;

			var maxNum = sizes.Max();
			var secNum = sizes.Where(f => f < maxNum).Max();
			if (sizes.Count(f => f < maxNum) <= sizes.Count - 2)
				secNum = maxNum;
			#endregion

			#region 第一轮
			var config = db.scb_WMS_LogisticsForWeight.Where(f => f.IsChecked == 1 && f.Sort == 1).ToList().FirstOrDefault();
			var weight_c = weight;
			if (config.Weight_Type == 1)
				weight_c = weight_vol;
			else if (config.Weight_Type == 2)
				weight_c = weight_phy;

			var ItemNo = sheets.First().cpbh;
			var smartposts = db.scb_WMS_MatchRule.Where(f => f.Type == "Allow_SmartPost" && f.State == 1).ToList();
			if (smartposts.Any(f => f.ItemNo.Contains(ItemNo + ",")))
			{
				OrderMatch.Logicswayoid = 18;
				OrderMatch.SericeType = "FEDEX_GROUND";
			}
			else if (weight < 1)
			{

			}
			else if (1 <= weight && weight < 9 && maxNum < 34 && secNum < 17)
			{
				var ups_weight = volNum2 / 300;
				var fedex_weight = volNum2 / 194;
				if (ups_weight > weight && fedex_weight > weight && ups_weight < 9 && fedex_weight < 9)
				{
					OrderMatch.Logicswayoid = 24;
					OrderMatch.SericeType = "UPSSurePost";
					return;
				}
				//if (ups_weight < weight && fedex_weight < weight && !disable.Any(f => f.oid == 21))
				//if (ups_weight < weight && fedex_weight < weight && db.scb_WMS_MatchRule.Any(f => f.Type == "Allow_SmartPost" && f.State == 1 && f.ItemNo.Contains(ItemNo + ",")))
				//{
				//    OrderMatch.Logicswayoid = 18;
				//    OrderMatch.SericeType = "FEDEX_GROUND";
				//    return;
				//}
				if (1 < ups_weight && ups_weight < 9 && fedex_weight >= 9)
				{
					OrderMatch.Logicswayoid = 24;
					OrderMatch.SericeType = "UPSSurePost";
					return;
				}
				if (ups_weight > weight && fedex_weight > weight && ups_weight >= 9 && fedex_weight >= 9)
				{
					OrderMatch.Logicswayoid = 4;
					OrderMatch.SericeType = "UPSGround";
					return;
				}
			}
			else if (weight_c >= double.Parse(config.Weight) && !disable.Any(f => f.oid == 18))
			{
				OrderMatch.Logicswayoid = 18;
				OrderMatch.SericeType = "FEDEX_GROUND";
				return;
			}
			else if (weight_c < double.Parse(config.Weight) && !disable.Any(f => f.oid == 18))
			{
				var maxNumMin = double.Parse(config.Chang.Split(',')[0]);
				var maxNumMax = double.Parse(config.Chang.Split(',')[1]);
				var secNumMin = double.Parse(config.Kuan);
				var volNumMin = double.Parse(config.Size.Split(',')[0]);
				var volNumMax = double.Parse(config.Size.Split(',')[1]);

				if ((maxNumMin < maxNum && maxNum < maxNumMax)
					|| (secNum > secNumMin)
					|| (volNumMin <= volNum && volNum < volNumMax))
				{
					OrderMatch.Logicswayoid = 18;
					OrderMatch.SericeType = "FEDEX_GROUND";
					return;
				}
			}

			#endregion

			#region 第二轮
			var current = new scb_LogisticsForWeight();
			if (weight_g > 0)
			{
				int ic = int.Parse(Math.Round(weight_g, MidpointRounding.AwayFromZero).ToString());
				var dtc2 = db.scb_LogisticsForWeight.Where(w => w.IsChecked == 1 && w.Warehouse == "Ontario").ToList();
				var dtc = new List<scb_LogisticsForWeight>();
				foreach (var w in dtc2)
				{
					if (int.Parse(w.Max) >= ic && int.Parse(w.Min) <= ic)
					{
						dtc.Add(w);
					}
				}
				if (dtc.Any())
				{
					bool flag = false;//判断标识
					foreach (var item in dtc)
					{
						if (item.LogisticsServiceMode == "UPSSurePost")
						{
							if (secNum <= double.Parse(item.Kuan) && maxNum <= double.Parse(item.Chang) && (volNum2) <= (double)item.Size && (volNum2) <= 2900)
							{
								current = item;
							}
							else
							{
								current.LogisticsID = 4;
								current.LogisticsServiceMode = "UPSGround";
							}
							flag = true;
							break;
						}
					}
					if (!flag)
					{
						current = dtc.FirstOrDefault();
					}
					OrderMatch.Logicswayoid = current.LogisticsID;
					if (current.LogisticsID == 3)
						OrderMatch.SericeType = "First|Default|" + current.LogisticsServiceMode + "|OFF";
					else
						OrderMatch.SericeType = current.LogisticsServiceMode;
					return;
				}


			}


			#endregion
		}

		private void MatchLogisticsV3(MatchModel model, ref OrderMatchResult OrderMatch)
		{
			var db = new cxtradeModel();
			var sheets = model.details;
			var sheet = sheets.FirstOrDefault();
			var good = db.N_goods.Where(g => g.cpbh == sheet.cpbh && g.country == model.country && g.groupflag == 0).FirstOrDefault();
			var entity = OrderMatch;
			var warehouse = db.scbck.Find(entity.WarehouseID);
			var current = new scb_LogisticsForWeight();

			double weight = 0;
			var sizes = new List<double?>();
			foreach (var sheet2 in sheets)
			{
				var good2 = db.N_goods.Where(g => g.cpbh == sheet.cpbh && g.country == model.country && g.groupflag == 0).FirstOrDefault();
				weight += (double)good.weight_express * sheet2.sl;
			}

			var country = model.country;
			if (weight > 0)
			{
				if (country == "US")
				{
					int ic = int.Parse(Math.Round((double)weight, MidpointRounding.AwayFromZero).ToString());
					var dtc2 = db.scb_LogisticsForWeight.Where(w => w.IsChecked == 1 && w.Warehouse == warehouse.realname).ToList();
					var dtc = new List<scb_LogisticsForWeight>();
					foreach (var w in dtc2)
					{
						if (int.Parse(w.Max) >= ic && int.Parse(w.Min) <= ic)
						{
							dtc.Add(w);
						}
					}
					if (dtc.Any())
					{
						double bzcd = (double)good.bzcd;
						double bzgd = (double)good.bzgd;
						double bzkd = (double)good.bzkd;
						if (bzcd != 0 && bzgd != 0 && bzkd != 0)
						{
							double maxNum = Math.Max(Math.Max(bzcd, bzkd), bzgd);//最大值
							double[] strSecond = new double[3] { bzcd, bzkd, bzgd };

							double maxValue, secondValue;
							GetMaxMiddValue(strSecond, out maxValue, out secondValue);

							bool flag = false;//判断标识
							foreach (var item in dtc)
							{
								if (item.LogisticsServiceMode == "UPSSurePost")
								{
									if (secondValue <= double.Parse(item.Kuan) && maxNum <= double.Parse(item.Chang) && (bzcd * bzkd * bzgd) <= (double)item.Size && (bzcd * bzkd * bzgd) <= 2900)
									{
										current = item;
									}
									else
									{
										current.LogisticsID = 4;
										current.LogisticsServiceMode = "UPSGround";
									}
									flag = true;
									break;
								}
							}
							if (!flag)
							{
								current = dtc.FirstOrDefault();
							}
							OrderMatch.Logicswayoid = current.LogisticsID;
							if (current.LogisticsID == 3)
								OrderMatch.SericeType = "First|Default|" + current.LogisticsServiceMode + "|OFF";
							else
								OrderMatch.SericeType = current.LogisticsServiceMode;
							return;
						}
					}
				}
			}
		}

		/// <summary>
		/// 新版美国匹配
		/// </summary>
		/// <param name="Order">订单信息</param>
		/// <param name="OrderMatch">发货仓库和物流(返回值)</param>
		private void MatchLogisticsByUSV2(scb_xsjl Order, List<string> _UPSSkuList, string datformat, ref OrderMatchResult OrderMatch, bool ignoreAMZN = false)
		{
			bool ISTest = false;

			#region 基础信息
			var db = new cxtradeModel();
			var sTime = DateTime.Now;
			var OpenLog = false;

			var zipsNO = _cacheZip.FirstOrDefault(z => Order.zip.StartsWith(z.DestZip));
			//var zipsNO = db.scb_ups_zip.FirstOrDefault(z => Order.zip.StartsWith(z.DestZip));
			if (zipsNO != null)
			{
				if ("HI,PR".Split(',').Contains(zipsNO.State))
				{
					OrderMatch.Result.Message = string.Format("匹配错误,州[{0}]不允许匹配！订单编号:{1}", Order.statsa, Order.number);
					return;
				}
			}
			if (OpenLog)
			{
				var t = Order.number + ":scb_ups_zip" + (DateTime.Now - sTime).TotalSeconds.ToString() + "秒";
				LogHelper.WriteTime(t);
				sTime = DateTime.Now;
			}

			var MatchRules = db.scb_WMS_MatchRule.Where(f => f.State == 1).ToList();//当前可以用的特殊匹配规则
			var Warehouses = db.Database.SqlQuery<ScbckModel>(@"select 
','+isnull((select cast(oid as nvarchar(50))+',' from scb_Logisticsmode where warehoues like '%'+scbck.id+'%' and IsEnable=1
FOR XML PATH('')),'') oids,number,realname, id, name, firstsend, AreaLocation, AreaSort, Origin, OriginSort,0 Sort,0 Levels, 
IsMatching, IsThird,0 EffectiveInventory  from scbck where countryid='01' and state=1 and  IsMatching=1 order by AreaSort").ToList();

			//if (Order.dianpu != "overstock-supplier")
			//{
			//    var removew = Warehouses.Where(o => o.id == "US17").FirstOrDefault();
			//    if (removew != null)
			//    {
			//        //,4,18,48,
			//        removew.oids = ",18,";
			//    }
			//}
			//三方订单仓库
			if (Order.name == "wms")
				Warehouses = Warehouses.Where(f => f.IsThird == 1).ToList();
			else
				Warehouses = Warehouses.Where(f => f.IsThird == 0).ToList();

			//var IsCAToUS = MatchRules.Any(f => f.Type == "CAToUS_Fedex");

			var sheets2 = db.scb_xsjlsheet.Where(f => f.father == Order.number).ToList();
			var sheetCpbh = sheets2.Select(f => f.cpbh).ToList();

			// if (Order.dianpu.ToLower() == "wayfair-ca" || Order.dianpu.ToLower() == "wayfair-gymax-ca")
			// {

			//     var order_carrier2 = db.Database.SqlQuery<scb_order_carrier>(string.Format("select * from scb_order_carrier where orderid='{0}' and dianpu='{1}' and oid is not null"
			//, Order.OrderID, Order.dianpu)).ToList();
			//     if (order_carrier2.Any())
			//     {
			//         var order_carrier = order_carrier2.First();
			//         OrderMatch.Logicswayoid = (int)order_carrier.oid;
			//         int WarehouseID = 0;
			//         try
			//         {
			//             WarehouseID = int.Parse(order_carrier.temp);
			//         }
			//         catch
			//         { }
			//         if (OrderMatch.Logicswayoid != 18 || WarehouseID != 82)
			//         {
			//             OrderMatch.Result.Message = $"wayfair-ca需要暂停发货,请手动处理!";
			//             return;
			//         }
			//     }
			//     else
			//     {
			//         OrderMatch.Result.Message = $"wayfair-ca需要暂停发货,请手动处理!";
			//         return;
			//     }
			// }

			#region 黑名单
			// 20231019 吴碧珊 21 KARENS WAY, BEAR, DE, 19701-5300这个地址麻烦先帮我设置下，拉黑不发的
			//20231120 史倩文 11 BELDEN PL 这个地址屏蔽一下吧
			//20231201 李积增 麻烦帮忙设置这个地址为黑名单。 10901 REED HARTMAN HWY STE 305, CINCINNATI, OH
			//20231220 吴碧珊 22 South Wind Drive 不发
			var blackAdressRule = MatchRules.FirstOrDefault(p => p.Type == "Blacklist" && p.Country == "US");
			var blackAdressList = db.scb_WMS_MatchRule_Details.Where(p => p.RID == blackAdressRule.ID).ToList();//new List<string>() { "10833 WILSHIRE BLVD", "21 KARENS WAY", "11 BELDEN PL", "10901 REED HARTMAN HWY STE 305", "22 South Wind Drive", "3901 CONSHOHOCKEN AVE APT 8301" };
			if (blackAdressList.Any(p => p.Parameter.ToUpper() == Order.adress.ToUpper()))
			{
				OrderMatch.Result.Message = $"此单疑似黑名单,请手动处理!";
				return;
			}
			#endregion
			#region 黑名单
			//美国本地罗马伞禁售
			var BlackList3 = new List<string>()
			{
				"OP70233","OP70234","OP70280","OP70376","NP10190","NP10191","NP10192","NP10287","NP10288"
			};
			if (Order.temp10.ToUpper() == "US")
			{
				foreach (var sheet in sheetCpbh)
				{
					foreach (var bl in BlackList3)
					{
						if (sheet.StartsWith(bl))
						{
							OrderMatch.Result.Message = $"{sheet}的美国禁售罗马伞货号，取消!";
							return;
						}
					}

				}
			}

			var BlackList = new List<string>()
			{
				"EP24045","EP24771US","EP24042","ES10182US-WH","ES10183US-WH","ES10173US-WH","EP24770US","EP24382US","EP20412","FP10119US-GR"
			};
			if (Order.temp10.ToUpper() == "US" && Order.statsa.ToUpper().StartsWith("CA"))
			{
				foreach (var sheet in sheetCpbh)
				{
					if (BlackList.Contains(sheet))
					{
						OrderMatch.Result.Message = $"{sheet}的CA不打单,请通知客服取消!";
						return;
					}
				}
			}
			//EP24897US-BK 吴碧芸说这个货可以发货了
			var BlackList2 = new List<string>()
			{
				"EP24897US-NY","EP24897US-SL","EP24897US-RE","NP11020US"
			};

			foreach (var sheet in sheetCpbh)
			{
				if (BlackList2.Contains(sheet) && (Order.dianpu.ToLower() != "costway-giveaway" && sheet != "EP24897US-RE"))
				{
					if (Order.dianpu.ToLower() == "costway-giveaway" && sheet == "EP24897US-RE")
					{

					}
					else
					{
						OrderMatch.Result.Message = $"{sheet}的全美不打单,请通知客服取消!";
						return;
					}
				}
			}
			var BabyCarNeedCancel = MatchRules.FirstOrDefault(f => f.Type == "BabyCarNeedCancel");
			var BabyCarNeedCancelCpbhs = db.scb_WMS_MatchRule_Details.Where(f => f.RID == BabyCarNeedCancel.ID && f.Type == "ItemNo").Select(o => o.Parameter).ToList();
			var BabyCarNeedCancelCpbh = sheetCpbh.Where(t => BabyCarNeedCancelCpbhs.Contains(t)).FirstOrDefault();
			if (BabyCarNeedCancelCpbh != null && (Order.temp10.ToUpper() == "CA" || Order.temp10.ToUpper() == "US"))
			{
				OrderMatch.Result.Message = $"此单存在婴儿车禁售全美{BabyCarNeedCancelCpbh},请取消!";
				return;
			}

			var BabyCarNeedCancelOnlyCA = MatchRules.FirstOrDefault(f => f.Type == "BabyCarNeedCancelCA");
			var BabyCarNeedCancelOnlyCACpbhs = db.scb_WMS_MatchRule_Details.Where(f => f.RID == BabyCarNeedCancelOnlyCA.ID && f.Type == "ItemNo").Select(o => o.Parameter).ToList();
			var BabyCarNeedCancelOnlyCACpbh = sheetCpbh.Where(t => BabyCarNeedCancelOnlyCACpbhs.Contains(t)).FirstOrDefault();
			if (BabyCarNeedCancelOnlyCACpbh != null && Order.temp10.ToUpper() == "CA")
			{
				OrderMatch.Result.Message = $"此单存在婴儿车禁售加拿大{BabyCarNeedCancelOnlyCACpbh},请取消!";
				return;
			}

			#endregion

			#region 判断是否为卡车单

			var Transportation = MatchRules.FirstOrDefault(f => f.Type == "Transportation");
			var TransportationCpbhs = db.scb_WMS_MatchRule_Details.Where(f => f.RID == Transportation.ID && f.Type == "ItemNo").Select(o => o.Parameter).ToList();
			var TransportationCpbh = sheetCpbh.Where(t => TransportationCpbhs.Contains(t)).FirstOrDefault();
			if (TransportationCpbh != null)
			{
				OrderMatch.Result.Message = $"此单存在卡车单货物{TransportationCpbh},请手动处理!";
				return;
			}
			#endregion
			var country = Order.country;
			var Tocountry = Order.temp10;
			var dianpuTrue = Order.dianpu;
			if (string.IsNullOrEmpty(datformat))
				datformat = DateTime.Now.ToString("yyyy-MM-dd");

			#region CanCAToUS 

			var CanCAToUS = false;

			var CanSendCAWarehouseMatchRule = MatchRules.FirstOrDefault(f => f.Type == "CAToUSWarehouse");
			var CanSendCAWarehouseMatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == CanSendCAWarehouseMatchRule.ID).Select(o => o.Parameter).ToList();

			#region 20240530 武金凤 关闭之前用部分sku和覆盖部分美国地区的规则并启用以下新的匹配规则  注释了
			//var NoStrockUSFromCA = false;
			//var CanSendCAToUSGoodMatchRule = MatchRules.FirstOrDefault(f => f.Type == "CAToUSGood");
			//var CanSendCAToUSGoodMatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == CanSendCAToUSGoodMatchRule.ID).Select(o => o.Parameter).ToList();
			//var cansendgoods = sheets2.Any(o => CanSendCAToUSGoodMatchRule_Details.Contains(o.cpbh));

			//var NoCanSendCAGood = MatchRules.FirstOrDefault(f => f.Type == "NoCAToUSGood");
			//var NoCanSendCAGood_Detail = db.scb_WMS_MatchRule_Details.Where(f => f.RID == NoCanSendCAGood.ID).Select(o => o.Parameter).ToList();

			//var AnyNoCanSendCAGood = sheets2.Where(o => NoCanSendCAGood_Detail.Contains(o.cpbh)).Any();
			//var CanSendALLUSGood = MatchRules.FirstOrDefault(f => f.Type == "CAToALLUSGood");
			//var CanSendALLUSGood_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == CanSendALLUSGood.ID).Select(o => o.Parameter).ToList();

			//var AnyCanSendALLUSGood = sheets2.Where(o => CanSendALLUSGood_Details.Contains(o.cpbh)).Any();
			//if ((Tocountry == "US" && cansendgoods) || (Tocountry == "US" && AnyCanSendALLUSGood))
			//{

			//	var OnlyUPS = false;
			//	//只用ups
			//	if (MatchRules.Any(f => f.Type == "Only_UPSGround_Send"))
			//	{
			//		var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_UPSGround_Send");
			//		var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
			//		if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == Order.dianpu.ToUpper()))
			//		{
			//			OnlyUPS = true;
			//		}
			//	}

			//	//店铺包含tiktok就不行
			//	var forbiddenCA_dianpus = new List<string>() { "walmart" }; //, "tiktok-giantex", "tiktok-deal", "tiktok-costzon"
			//	if (AnyCanSendALLUSGood)
			//	{
			//		CanCAToUS = !OnlyUPS && (Order.xspt.ToLower() != "shein") && (Order.xspt.ToLower() != "target") && (!forbiddenCA_dianpus.Contains(Order.dianpu.ToLower())) && (!Order.dianpu.ToLower().Contains("tiktok")) && CanSendCAWarehouseMatchRule_Details.Any() && !AnyNoCanSendCAGood;
			//	}
			//	else if (cansendgoods)
			//	{
			//		var CanSendCAStateMatchRule = MatchRules.FirstOrDefault(f => f.Type == "CAToUSState");
			//		var CanSendCAStateMatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == CanSendCAStateMatchRule.ID && f.Type == "State").Select(o => o.Parameter).ToList();
			//		var CanSendCATozips = _cacheZip.FirstOrDefault(z => Order.zip.StartsWith(z.DestZip));
			//		if (CanSendCATozips != null)
			//		{
			//			//金兰 2022/08/04 Target 和Target topbuy 帮我拦截下 不要走加拿大去美国的路线
			//			//刘志明 2023/01/17 指定UPS的不从加拿大发货
			//			////20240418 wjf temu禁发FedExTOUS，不能发加拿大仓
			//			CanCAToUS = !OnlyUPS && (Order.xspt.ToLower() != "shein") && (Order.xspt.ToLower() != "target") && (Order.xspt.ToLower() != "temu") && (Order.xspt.ToLower() != "walmart") && (!forbiddenCA_dianpus.Contains(Order.dianpu.ToLower())) && (!Order.dianpu.ToLower().Contains("tiktok")) && CanSendCAStateMatchRule_Details.Where(o => o == CanSendCATozips.State.ToUpper()).Any() && CanSendCAWarehouseMatchRule_Details.Any() && !AnyNoCanSendCAGood;
			//		}
			//		else
			//		{
			//			OrderMatch.Result.Message = string.Format("匹配错误,邮编[{0}]无效！订单编号:{1}", Order.zip, Order.number);
			//			return;
			//		}
			//	}
			//}
			#endregion

			var ban_FedexToUS = MatchRules.FirstOrDefault(p => p.Type == "NoCAToUSGood");
			var banFedexToUSGood_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == ban_FedexToUS.ID && f.Type == "ItemNo").Select(o => o.Parameter).ToList();

			var OnlyUPS = false;
			//只用ups
			if (MatchRules.Any(f => f.Type == "Only_UPSGround_Send"))
			{
				var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_UPSGround_Send");
				var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
				if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == Order.dianpu.ToUpper()))
				{
					OnlyUPS = true;
				}
			}
			//	//店铺包含tiktok就不行
			//var forbiddenCA_dianpus = new List<string>() { "walmart" }; //, "tiktok-giantex", "tiktok-deal", "tiktok-costzon"
			var AnyNoCanSendCAGood = sheets2.Where(o => banFedexToUSGood_Details.Contains(o.cpbh)).Any();

			//if (Tocountry == "US" && !AnyNoCanSendCAGood && !OnlyUPS && CanSendCAWarehouseMatchRule_Details.Any() && !forbiddenCA_xspts.Any(p => p.ToLower() == Order.xspt.ToLower()))
			//{
			//	//var dat = DateTime.Now;
			//	//var count = db.Database.SqlQuery<int>(string.Format($@"select isnull(SUM(x.zsl),0) from scb_xsjl x 
			//	//            where country = 'US' and x.temp10 = 'US'
			//	//            and x.state = 0 and filterflag > 5 AND x.newdateis>'{dat.ToString("yyyy-MM-dd")}' AND x.newdateis<'{dat.AddDays(1).ToString("yyyy-MM-dd")}'
			//	//			AND x.hthm NOT LIKE 'Order%' AND x.hthm NOT LIKE 'return%'AND x.logicswayoid in (79) and x.warehouseid in (141)")).FirstOrDefault();
			//	//if (count <= 400)
			//	//{
			//	//	CanCAToUS = true;
			//	//}
			//	CanCAToUS = true;
			//}

			#endregion

			#region 分体式空调			
			var isAircondition = false;
			var aircondition_box = string.Empty;
			var airconditionrule = MatchRules.FirstOrDefault(p => p.Type == "UnPack_AirconditionPart");
			if (sheets2.Sum(p => p.sl) > 1 && airconditionrule != null)
			{
				var airconditionDetail = db.scb_WMS_MatchRule_Details.Where(p => p.RID == airconditionrule.ID).ToList();
				var airconditionParts = airconditionDetail.Where(p => p.Type == "part").Select(p => p.Parameter).ToList();
				if (sheets2.Where(p => airconditionParts.Contains(p.cpbh)).Sum(p => p.sl) > 1)
				{
					isAircondition = true;
					aircondition_box = airconditionDetail.Where(p => p.Type == "box").Select(p => p.Parameter).FirstOrDefault();
				}
				if (isAircondition)
				{
					if (string.IsNullOrEmpty(aircondition_box))
					{
						OrderMatch.Result.Message = $"未获取到空白外箱货号，请联系运营确认！";
						return;
					}
					var notparts = sheets2.Where(p => !airconditionParts.Contains(p.cpbh)).Select(p => p.cpbh);
					if (notparts.Any())
					{
						OrderMatch.Result.Message = $"存在非分布式空调的配件：{string.Join(",", notparts.ToList())} 请重新拆单";
						return;
					}
				}
			}
			#endregion

			#endregion

			#region 固定/指定物流
			bool IsInternational = false;
			//国际件
			if (country == "US" && (Tocountry == "CA" || Tocountry == "MX") && !dianpuTrue.ToLower().Contains("grouponvendor-ca"))
			{
				OrderMatch.Logicswayoid = 48;
				OrderMatch.SericeType = "UPSInternational";
				IsInternational = true;
			}

			#region 指定物流
			bool IsFind = true;
			bool IsAppoint = false;
			if (IsFind)
			{
				var order_carrier2 = db.Database.SqlQuery<scb_order_carrier>(string.Format("select * from scb_order_carrier where orderid='{0}' and dianpu='{1}' and oid is not null"
					, Order.OrderID, Order.dianpu)).ToList();
				if (order_carrier2.Any())
				{
					IsAppoint = true;
					var order_carrier = order_carrier2.First();
					OrderMatch.Logicswayoid = (int)order_carrier.oid;
					if (string.IsNullOrEmpty(order_carrier.ShipService))
					{
						if (OrderMatch.Logicswayoid == 4)
							order_carrier.ShipService = "UPSGround";
						else if (OrderMatch.Logicswayoid == 18)
							order_carrier.ShipService = "FEDEX_GROUND";

					}
					OrderMatch.SericeType = order_carrier.ShipService;
					//部分CA指定物流是UPS，我们这边就需要转成48，CA仓的UPS==UPSInternational
					if (country == "US" && (Tocountry == "CA" || Tocountry == "MX") && OrderMatch.Logicswayoid == 4)
					{
						OrderMatch.Logicswayoid = 48;
						OrderMatch.SericeType = "UPSInternational";
						IsInternational = true;
					}
					if (Order.dianpu.ToLower() == "fdsus" && (int)order_carrier.oid == 22)
					{
						OrderMatch.Result.Message = $"此单是{Order.dianpu}卡车单,请手动处理!";
						return;
					}
					//20231019 吴碧珊 针对wayfair平台补发重发不用指定仓库，无需提示，正常匹配就可以了
					if (Order.xspt.ToLower() == "wayfair" && !string.IsNullOrEmpty(order_carrier.temp) && Order.temp3 != "8" && Order.temp3 != "4")
					{
						int WarehouseID = int.Parse(order_carrier.temp);
						var wayfairItems = db.scb_xsjlsheet.Where(f => f.father == Order.number).ToList();
						var WarehouseName = db.scbck.FirstOrDefault(f => f.number == WarehouseID).id;
						foreach (var item in wayfairItems)
						{
							var CurrentItemNo = item.cpbh;
							var sql = string.Format(@"select Warehouse WarehouseNo,kcsl-tempkcsl-unshipped EffectiveInventory,
kcsl ,tempkcsl,unshipped,
(select count(1) from scb_WMS_MatchRule a inner join scb_WMS_MatchRule_Details b on 
a.ID=b.RID and b.Type='ItemNo' and a.Type='FDS_StockItemNo' and a.State=1 and b.Parameter='{0}') FDS_StockItemNo,
isnull((select top 1 cast(Parameter as int) from scb_WMS_MatchRule a inner join scb_WMS_MatchRule_Details b on 
a.ID=b.RID and b.Type=t.Warehouse and a.Type='FDS_StockWarehouse' and a.State=1),0) FDS_Stock
from (
select Warehouse, SUM(sl) kcsl,
isnull((select SUM(sl) tempkcsl from scb_tempkcsl where nowdate = '{2}' and ck = Warehouse and cpbh = '{0}'), 0) tempkcsl,
isnull((select SUM(qty)  from tmp_pickinglist_occupy where cangku = Warehouse and cpbh = '{0}'), 0) unshipped
from scb_kcjl_location a inner join scb_kcjl_area_cangw b
on a.Warehouse = b.Location and a.Location = b.cangw
where b.state = 0 and a.Disable = 0  and  a.LocationType<10 and b.cpbh = '{0}' and a.Warehouse='{3}'
group by Warehouse) t where kcsl - tempkcsl - unshipped >= {1}", CurrentItemNo, item.sl, datformat, WarehouseName);
							var list = db.Database.SqlQuery<WarehouseDetail>(sql).ToList();
							if (!list.Any())
							{
								//库存不足
								//var goodssheet = db.N_goodsSheet.Where(f => f.ItemNo1 == CurrentItemNo && (f.Country == "US" || f.Country == "CA")).ToList();
								//if (goodssheet.Any())
								//{
								//    var items = string.Empty;
								//    foreach (var replaceitem in goodssheet.OrderBy(f => f.Sort))
								//    {
								//        var kcjl = db.scb_kcjl_area.Where(f => f.cpbh == replaceitem.ItemNo2 && f.place.Contains("US") && f.place == WarehouseName).ToList();
								//        if (kcjl.Any())
								//        {
								//            items += string.Format("{0}({1})({2}),", replaceitem.ItemNo2, kcjl.OrderBy(f => f.place).FirstOrDefault().place, replaceitem.Country);
								//        }
								//        else
								//        {
								//            items += replaceitem.ItemNo2 + ",";
								//        }
								//    }

								//    OrderMatch.Result.Message += string.Format("wayfair平台 ，{1}，无库存， 推荐更换货号:{0}", items.TrimEnd(','), WarehouseName);
								//    return;
								//}
								string oldCpbh = CurrentItemNo;
								string newCpbh = RecommendCpbh(Order.temp10, Order.dianpu, oldCpbh, item.sl, Order.country, Order.xspt);
								if (oldCpbh != newCpbh)
								{
									OrderMatch.Result.Message += string.Format(" 推荐更换货号:{0}", newCpbh.TrimEnd(','));
									return;
								}
								else
								{
									//if (OrderMatch.SericeType != "Transportation-wayfair")
									//{
									//    order_carrier.temp = "";
									//    continue;
									//}
									OrderMatch.Result.Message += string.Format("wayfair平台 ，货号:{0}，{1} 指定仓库没货", oldCpbh, WarehouseName);
									return;
								}

							}
						}
					}

					if (OrderMatch.SericeType == "Transportation-wayfair")
					{
						//已指定物流和仓库 不需要继续匹配了
						int WarehouseID = int.Parse(order_carrier.temp);
						OrderMatch.WarehouseID = WarehouseID;
						OrderMatch.WarehouseName = db.scbck.FirstOrDefault(f => f.number == WarehouseID).id;
						return;
					}
					//20231019 吴碧珊 针对wayfair平台补发重发不用指定仓库，无需提示，正常匹配就可以了
					//20250804 吴碧珊 针对wayfair平台补发重发使用指定仓库
					if (!string.IsNullOrEmpty(order_carrier.temp) && order_carrier.dianpu.ToLower().Contains("skonyon"))//(order_carrier.dianpu.ToLower().Contains("wayfair") && Order.temp3 != "4" && Order.temp3 != "8") 
					{
						//已指定物流和仓库 不需要继续匹配了
						int WarehouseID = int.Parse(order_carrier.temp);
						OrderMatch.WarehouseID = WarehouseID;
						OrderMatch.WarehouseName = db.scbck.FirstOrDefault(f => f.number == WarehouseID).id;
						return;
					}

				}
			}
			#endregion

			#region 特殊指定
			if (!IsAppoint)
			{
				//只用fedex ItemNo
				if (MatchRules.Any(f => f.Type == "Only_FedexGround_Send"))
				{
					var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_FedexGround_Send");
					var MatchRule_Details2 = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "ItemNo").ToList();
					var ItemNo = sheets2.FirstOrDefault().cpbh;
					if (MatchRule_Details2.Any(f => f.Parameter.ToUpper() == ItemNo.ToUpper()))
					{
						OrderMatch.Logicswayoid = 18;
						OrderMatch.SericeType = "FEDEX_GROUND";
					}
				}

				//只用ups
				if (MatchRules.Any(f => f.Type == "Only_UPSGround_Send"))
				{
					var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_UPSGround_Send");
					var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
					if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == Order.dianpu.ToUpper()))
					{
						OrderMatch.Logicswayoid = 4;
						OrderMatch.SericeType = "UPSGround";
					}
				}
				//只用fedex
				if (MatchRules.Any(f => f.Type == "Only_FedexGround_Send"))
				{
					var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_FedexGround_Send");
					var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
					if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == Order.dianpu.ToUpper()))
					{
						OrderMatch.Logicswayoid = 18;
						OrderMatch.SericeType = "FEDEX_GROUND";
					}
				}

				if (MatchRules.Any(f => f.Type == "Walmart_NextDay"))
				{
					var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Walmart_NextDay");
					if (MatchRule.Shop.Split(',').Contains(Order.dianpu) && Order.temp2 == "1")
					{
						OrderMatch.Logicswayoid = 4;
						OrderMatch.SericeType = "UPSGround";
					}
				}
			}

			if ("WalmartVendor,wayfair".ToUpper().Split(',').Contains(Order.dianpu.ToUpper()))
			{
				foreach (var item in Warehouses)
				{
					item.oids = item.oids.Replace(",24,", ",");
				}
			}
			#endregion

			#region 仓库物流禁用ups和fedex互换
			if (string.IsNullOrEmpty(Order.trackno))
			{
				if (MatchRules.Any(f => f.Type == "Ban_Fedex_ConvertUPSGround")
				&& OrderMatch.Logicswayoid == 18 && OrderMatch.SericeType == "FEDEX_GROUND")
				{
					var IsOk = false;
					var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Ban_Fedex_ConvertUPSGround");
					var MatchRule_DetailsByStore = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
					if (MatchRule_DetailsByStore.Any(f => f.Parameter.ToUpper() == Order.dianpu.ToUpper()))
					{
						IsOk = true;
					}
					var MatchRule_DetailsByItem = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Item").ToList();
					if (MatchRule_DetailsByStore.Any(f => f.Parameter.ToUpper() == Order.dianpu.ToUpper()))
					{
						IsOk = true;
					}
					if (IsOk)
					{
						foreach (var item in Warehouses)
						{
							item.oids = item.oids.Replace(",18,", ",");
						}
					}
				}
			}

			#endregion


			#endregion

			#region 初始化重量尺寸等
			var sheets = db.scb_xsjlsheet.Where(f => f.father == Order.number).ToList();
			double weight = 0;
			double weightReal = 0;
			double weight_g = 0;
			double? volNum = 0;
			double? volNum2 = 0;
			var cpRealSizeinfos = new List<CpbhSizeInfo>();
			var sizes = new List<double?>();
			foreach (var sheet in sheets)
			{

				#region yushu 货号 是预售产品
				var isExsitPrecbph = false;
				#region 老产品
				if (!isExsitPrecbph)
				{
					var xspt = Order.xspt;
					if (xspt.ToUpper() == "AM")
					{
						xspt = "amazon";
					}
					var isOldPrecbph = db.scb_Presale_Goods.Where(d => d.IsDelete == 0 && (d.country == "US" || d.country == "CA") && d.cpbh == sheet.cpbh && (d.dianpu == Order.dianpu || string.IsNullOrEmpty(d.dianpu)) && (d.platform == xspt || string.IsNullOrEmpty(d.platform)) && d.starttime < DateTime.Now && (!d.endtime.HasValue || (d.endtime.Value > DateTime.Now))).Any();
					if (isOldPrecbph)
					{
						isExsitPrecbph = true;
					}
				}
				#endregion
				#region 新产品

				if (!isExsitPrecbph)
				{
					var Precbph = db.scb_oms_new_prd.Where(d => d.cpbh == sheet.cpbh && d.country == "US" && !d.is_stock).Any();
					if (Precbph)
					{
						isExsitPrecbph = true;
					}
				}
				#endregion
				if (isExsitPrecbph)
				{
					OrderMatch.Result.Message = string.Format("预售货号,请手动处理");
					return;
				}
				#endregion

				var good = db.N_goods.Where(g => g.cpbh == sheet.cpbh && g.country == country && g.groupflag == 0).FirstOrDefault();
				var cpRealSizeinfo = new CpbhSizeInfo() { cpbh = sheet.cpbh, qty = sheet.sl };
				cpRealSizeinfo.bzcd = (decimal)good.bzcd;
				cpRealSizeinfo.bzkd = (decimal)good.bzkd;
				cpRealSizeinfo.bzgd = (decimal)good.bzgd;
				cpRealSizeinfo.weight = (decimal)good.weight * sheet.sl / 1000 * 2.2046226m;
				var realsize = db.scb_realsize.Where(p => p.cpbh == sheet.cpbh && p.country == country).FirstOrDefault();
				if (realsize != null)
				{
					cpRealSizeinfo.bzcd = (decimal)realsize.bzcd;
					cpRealSizeinfo.bzkd = (decimal)realsize.bzkd;
					cpRealSizeinfo.bzgd = (decimal)realsize.bzgd;
				}
				cpRealSizeinfos.Add(cpRealSizeinfo);

				sizes.Add(good.bzgd);
				sizes.Add(good.bzcd);
				sizes.Add(good.bzkd);
				weightReal += (double)good.weight * sheet.sl / 1000 * 2.2046226;
				weight += (double)good.weight_express * sheet.sl / 1000 * 2.2046226;
				weight_g += (double)good.weight_express * sheet.sl;
				volNum += (good.bzcd + 2 * (good.bzkd + good.bzgd)) * sheet.sl;
				volNum2 += good.bzcd * good.bzgd * good.bzkd * sheet.sl;

				var sizesum = sizes.Sum(f => f) * 2 - sizes.Max();
				if (sizesum > 165)
				{
					OrderMatch.Result.Message = string.Format("匹配错误,尺寸{0}>165 超过标准,请手动处理", sizesum);
					return;
				}
				if (weight > 150)
				{
					OrderMatch.Result.Message = string.Format("匹配错误,实重{0}>150 超过标准,请手动处理", weight);
					return;
				}
				if (sizes.Max() > 108)
				{
					OrderMatch.Result.Message = string.Format("匹配错误,长{0}>108 超过标准,请手动处理", sizes.Max());
					return;
				}
			}

			if (isAircondition)
			{
				var box = db.N_goods.Where(g => g.cpbh == aircondition_box && g.country == country && g.groupflag == 0).FirstOrDefault();
				if (box == null)
				{
					OrderMatch.Result.Message = string.Format("无分体式空调的空白纸箱 {0}的产品信息，请确认", aircondition_box);
					return;
				}
				cpRealSizeinfos = new List<CpbhSizeInfo>() { new CpbhSizeInfo() {
					bzcd = (decimal)box.bzcd,
					bzkd = (decimal)box.bzkd,
					bzgd = (decimal)box.bzgd,
					weight =(decimal)weightReal
				} };
				sizes = new List<double?>() { box.bzcd, box.bzkd, box.bzgd };
				volNum = (box.bzcd + 2 * (box.bzkd + box.bzgd)) * 1;
				volNum2 += box.bzcd * box.bzgd * box.bzkd * 1;
			}

			double weight_vol = (double)volNum2 / 250;
			double weight_phy = weight_vol > weight ? weight_vol : weight;
			double? girth_in = 0;

			var maxNum = sizes.Max();
			var secNum = sizes.Where(f => f < maxNum).Max();
			if (sizes.Count(f => f < maxNum) <= sizes.Count - 2)
				secNum = maxNum;

			#endregion

			#region 邮编校验+偏远邮编校验 这些地址无法匹配
			if (MatchRules.Any(f => f.Type == "Remote_Districts"))
			{
				var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Remote_Districts");
				var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
				if (!MatchRule_Details.Any(f => f.Parameter.ToUpper() == dianpuTrue.ToUpper()) && Tocountry == "US")
				{
					var statsa = string.Empty;
					var zips = _cacheZip.FirstOrDefault(z => Order.zip.StartsWith(z.DestZip));
					if (zips != null)
					{
						if ("AK,HI,PR".Split(',').Contains(zips.State))
						{
							OrderMatch.Result.Message = string.Format("匹配错误,州[{0}]不允许匹配！订单编号:{1}", Order.statsa, Order.number);
							return;
						}
					}
					else
					{
						OrderMatch.Result.Message = string.Format("匹配错误,邮编[{0}]无效！订单编号:{1}", Order.zip, Order.number);
						return;
					}
				}

				var MatchRulePobox = MatchRules.FirstOrDefault(f => f.Type == "Remote_DistrictsByPobox");
				var MatchRulePobox_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
				if (!MatchRule_Details.Any(f => f.Parameter.ToUpper() == dianpuTrue.ToUpper()))
				{
					if (Order.adress.ToLower().Replace(" ", "").Replace(".", "").Contains("pobox"))
					{
						OrderMatch.Result.Message = string.Format("匹配错误,PO Box不允许发货！订单编号:{1}", Order.statsa, Order.number);
						return;
					}
				}
			}

			#endregion

			#region 指定仓库openbox
			if (!string.IsNullOrEmpty(Order.chck))
			{
				if (Order.chck.ToLower() == "openbox")
				{
					OrderMatch.WarehouseID = 30;
					OrderMatch.WarehouseName = "US95";
				}
			}

			#endregion

			#region 跑步机订单时间14号至16号只允许从最近两日内的仓库发货
			bool IsTwoDays = false;

			if (MatchRules.Any(f => f.Type == "TwoDays") && Order.temp10 == "US")
			{
				var TwoDays = MatchRules.FirstOrDefault(f => f.Type == "TwoDays");
				var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == TwoDays.ID).ToList();
				var startdate = new DateTime(2021, 09, 14);
				var enddate = startdate.AddDays(3);
				if (Order.orderdate >= startdate && Order.orderdate < enddate && MatchRule_Details.Any(f => sheets2.Any(g => g.cpbh == f.Parameter)))
				{
					IsTwoDays = true;
				}
			}
			#endregion

			#region 匹配仓库并排序
			//存在任何美国仓库有货的标识
			//var IsExsitStockWarehouse = false;
			var ws = new Dictionary<decimal, List<ScbckModel>>();
			var states = string.Empty;
			if (string.IsNullOrEmpty(OrderMatch.WarehouseName))
			{
				lock (MatchWarehouseLock)
				{
					#region 仓库清单
					var sort_i = 0;
					var WarehouseIDList = new List<string>();
					var WarehouseList = new List<ScbckModel>();
					var cklist = Warehouses.Where(c => c.Origin != "").ToList();
					if (!string.IsNullOrEmpty(Order.zip))
					{
						var zips = _cacheZip.Where(z => Order.zip.StartsWith(z.DestZip)).ToList();

						if (zips.Any())
						{
							if (IsTwoDays)
							{
								var mindays = zips.Min(g => g.TNTDAYS) + 1;
								zips = zips.Where(f => f.TNTDAYS <= mindays).ToList();

								if (!zips.Any())
								{
									OrderMatch.Result.Message = "跑步机系列无就近仓库有库存";
									return;
								}
							}

							zips = zips.OrderBy(z => z.GNDZone).ThenBy(z => z.TNTDAYS).ToList();
							if (string.IsNullOrEmpty(states))
								states = zips.First().State;
							foreach (var item in zips)
							{
								var Origin = item.Origin;
								var ct = cklist.Where(c => c.Origin == Origin).OrderBy(c => c.OriginSort);

								foreach (var item2 in ct)
								{
									sort_i += 1;
									item2.Sort = sort_i;

									WarehouseList.Add(item2);
									WarehouseIDList.Add(item2.id);
								}
							}
							Helper.Info(Order.number, "wms_api", $"订单编号:{Order.number},根据scb_ups_zip表，符合要求的仓库有：{string.Join(",", WarehouseIDList)}");
						}
					}

					if (IsInternational)
					{
						var restockCK = db.scbck.Where(o => o.countryid == "01" && o.realname.Contains("restock") && o.id != "US121").Select(o => o.id).ToList();
						WarehouseList = cklist.Where(f => f.id != "US98").ToList();
						WarehouseList = WarehouseList.Where(f => !restockCK.Contains(f.id)).ToList();
					}


					var lcks = new List<ScbckModel>();

					////清仓-暂时屏蔽
					//var firstsend = cklist.Where(c => c.id.StartsWith("US") && c.firstsend == 2);
					//if (firstsend.Any() && Order.temp10 == "US")
					//    lcks.AddRange(firstsend);


					if (WarehouseList.Any())
					{
						if (IsInternational)
						{

							if (Tocountry == "CA")
							{
								var CA_Fedex = MatchRules.FirstOrDefault(f => f.Type == "CA_Fedex");
								var CA_Fedexs = new List<scb_WMS_MatchRule_Details>();
								if (CA_Fedex != null)
								{
									CA_Fedexs = db.scb_WMS_MatchRule_Details.Where(f => f.RID == CA_Fedex.ID && f.Type == "Warehouse").ToList();
									WarehouseList.Where(f => CA_Fedexs.Any(g => g.Parameter == f.id)).ToList().ForEach(x => { WarehouseList.Remove(x); });
								}
								if (Warehouses.Any(c => c.id == "US121"))
								{
									var tt = Warehouses.FirstOrDefault(c => c.id == "US121");
									tt.Sort = -99;
									lcks.Add(tt);
								}
								if (Warehouses.Any(c => c.id == "US21"))
								{
									var tt = Warehouses.FirstOrDefault(c => c.id == "US21");
									tt.Sort = -97;
									lcks.Add(tt);
								}

								if (CA_Fedexs.Any())
								{
									//CA的仓库排序靠AreaSort来的，在这边处理不会破坏他原有排序。
									var t = cklist.Where(c => CA_Fedexs.Any(g => g.Parameter == c.id)).ToList();
									int Sort_i = -95;
									foreach (var item in t)
									{
										Sort_i += 1;
										item.Sort = Sort_i;
										//if (item.id == "US17")
										//{
										//    item.Sort = 1;
										//}
										//else if (item.id == "US11")
										//{
										//    item.Sort = 2;
										//}
										//else if (item.id == "US16")
										//{
										//    item.Sort = 3;
										//}
										//else if (item.id == "US06")
										//{
										//    item.Sort = 4;
										//}
										lcks.Add(item);
									}
									// lcks = lcks.OrderBy(p => p.Sort).ToList();
								}

								//CA部分邮编禁止发Fedex 相当于只能从US21发货
								var CANoZip = MatchRules.FirstOrDefault(f => f.Type == "CANoZip");
								if (CANoZip != null)
								{
									var CANoZips = db.scb_WMS_MatchRule_Details.Where(f => f.RID == CANoZip.ID && f.Type == "ZIP").ToList();
									if (CANoZips.Any(f => Order.zip.StartsWith(f.Parameter)))
									{
										OrderMatch.Result.Message = string.Format("匹配错误,加拿大BC省特殊邮编[{0}]禁止发货！订单编号:{1}", Order.zip, Order.number);
										return;
									}
								}
								//对于目的地在YT、NT、NU的Intlocal订单，只能从US11发货，US17、US16、US06不能发。也就只能用Fedex发
								//2022-04-11US16和US06也用fedex打，所以去除代码判断
								//20240424 张洁楠 US11不发Fedex
								var CANoUPS = MatchRules.FirstOrDefault(f => f.Type == "CANoUPS");
								if (CANoUPS != null)
								{
									var CANoUPSs = db.scb_WMS_MatchRule_Details.Where(f => f.RID == CANoUPS.ID && f.Type == "ZIP").ToList();
									if (CANoUPSs.Any(f => Order.zip.StartsWith(f.Parameter)))
									{
										if (Warehouses.Any(c => c.id == "US17" || c.id == "US11"))
										{
											Warehouses = Warehouses.Where(f => f.id != "US17" && f.id != "US11").ToList();
											lcks = lcks.Where(f => f.id != "US17" && f.id != "US11").ToList();
										}
										//if (Warehouses.Any(c => c.id == "US16"))
										//{
										//    Warehouses = Warehouses.Where(f => f.id != "US16").ToList();
										//    lcks = lcks.Where(f => f.id != "US16").ToList();
										//}
										//if (Warehouses.Any(c => c.id == "US06"))
										//{
										//    Warehouses = Warehouses.Where(f => f.id != "US06").ToList();
										//    lcks = lcks.Where(f => f.id != "US06").ToList();
										//}
									}
								}

							}
							else if (Tocountry == "MX")
							{
								if (Warehouses.Any(c => c.id == "US121"))
								{
									Warehouses = Warehouses.Where(o => o.id != "US121").ToList();
									WarehouseList = WarehouseList.Where(o => o.id != "US121").ToList();
								}
								if (Warehouses.Any(c => c.id == "US21"))
								{
									Warehouses = Warehouses.Where(o => o.id != "US21").ToList();
									WarehouseList = WarehouseList.Where(o => o.id != "US21").ToList();
								}
							}
						}
						else
						{
							//west加入restock仓库匹配
							if (WarehouseList.First().AreaLocation == "west")
							{
								var us98 = Warehouses.FirstOrDefault(c => c.id == "US98");
								if (us98 != null)
								{
									us98.Sort = -50;
									lcks.Add(us98);
								}
							}
							#region 仓库顺序放到最后
							var Last_Warehouses = MatchRules.FirstOrDefault(f => f.Type == "Last_Warehouses");
							if (Last_Warehouses != null)
							{
								var warehouses = Last_Warehouses.Warehouse.Split(',');
								foreach (var x in warehouses)
								{
									var last = WarehouseList.FirstOrDefault(f => f.id == x);
									if (last != null)
									{
										WarehouseList.Remove(last);
										WarehouseList.Add(last);
									}
								}
							}
							#endregion
						}

						lcks.AddRange(WarehouseList);
					}
					else
						lcks.AddRange(WarehouseList);


					// FDSUS店铺不发us98 restock库存产品
					var Ban_Restock = MatchRules.FirstOrDefault(f => f.Type == "Ban_Restock");
					if (Ban_Restock != null)
					{
						var Ban_Restocks = db.scb_WMS_MatchRule_Details.Where(f => f.RID == Ban_Restock.ID && f.Type == "Store").ToList();
						if (Ban_Restocks.Any(f => f.Parameter == Order.dianpu))
						{
							lcks = lcks.Where(f => f.id != "US98").ToList();
						}
					}

					if (Order.dianpu == "Costway-Jandy")
					{
						if (Warehouses.Any(f => f.id == "US07" || f.id == "US11"))
							Warehouses = Warehouses.Where(f => f.id == "US07" || f.id == "US11").ToList();
					}


					if (OrderMatch.SericeType == null)
						OrderMatch.SericeType = string.Empty;

					#endregion

					#region 分配

					#region 当前店铺配额是否足够
					if (ISTest)
					{

						foreach (var item in sheets2)
						{
							var IsCheck = db.Database.SqlQuery<int>(string.Format(@"
select count(1) from BUStock_Detail a left join BUStock_GroupDetail b on a.GroupID=b.GroupID
where a.ItemNo='{0}' and b.Dianpu='{1}' and WarehousingQty>0", item.cpbh, Order.dianpu)).ToList().First();

							if (IsCheck > 0)
							{
								var gcount = db.Database.SqlQuery<int>(
									string.Format(@"select count(1) from (
select SUM(WarehousingQty-DeliverQty+InQty-OutQty+InventoryQty) Qty,
isnull((select SUM(OccupyQty) from BUStock_WarhouseOccupy f  where f.GroupID=max(a.GroupID) and f.ItemNo=max(a.ItemNo) and TimeGet='{3}'),0) OccQty,
isnull((select SUM(qty) from ods_wms_pickinglist_preparelabel where timeget>='{3}' and sn>=-1 and sn<8 and sign=0 and issuspend=1 and cpbh='{0}'
and exists(select 1 from BUStock_GroupDetail c where c.GroupID=max(a.GroupID) and c.Dianpu=ods_wms_pickinglist_preparelabel.shop) ),0) OccQty2
 from BUStock_Detail a left join BUStock_GroupDetail b on a.GroupID=b.GroupID
where a.ItemNo='{0}' and b.Dianpu='{1}' and WarehousingQty>0) t where Qty-OccQty-OccQty2>={2}"
		, item.cpbh, Order.dianpu, item.sl, datformat)).ToList().First();
								if (gcount == 0)
								{
									OrderMatch.Result.Message = string.Format("店铺{0} 货号{1}配额数量不足，请购买", Order.dianpu, item.cpbh);
									return;
								}
							}
						}
					}
					#endregion

					var area = new List<int>();

					foreach (var item in sheets2)
					{
						var CurrentItemNo = item.cpbh;
						var sql = string.Format(@"select Warehouse WarehouseNo,kcsl-tempkcsl-unshipped EffectiveInventory,
kcsl ,tempkcsl,unshipped,
(select count(1) from scb_WMS_MatchRule a inner join scb_WMS_MatchRule_Details b on 
a.ID=b.RID and b.Type='ItemNo' and a.Type='FDS_StockItemNo' and a.State=1 and b.Parameter='{0}') FDS_StockItemNo,
isnull((select top 1 cast(Parameter as int) from scb_WMS_MatchRule a inner join scb_WMS_MatchRule_Details b on 
a.ID=b.RID and b.Type=t.Warehouse and a.Type='FDS_StockWarehouse' and a.State=1),0) FDS_Stock
from (
select Warehouse, SUM(sl) kcsl,
isnull((select SUM(sl) tempkcsl from scb_tempkcsl where nowdate = '{2}' and ck = Warehouse and cpbh = '{0}'), 0) tempkcsl,
isnull((select SUM(qty)  from tmp_pickinglist_occupy where cangku = Warehouse and cpbh = '{0}'), 0) unshipped
from scb_kcjl_location a inner join scb_kcjl_area_cangw b
on a.Warehouse = b.Location and a.Location = b.cangw
where b.state = 0 and a.Disable = 0  and  a.LocationType<10 and b.cpbh = '{0}'
group by Warehouse) t where kcsl - tempkcsl - unshipped >= {1}", CurrentItemNo, item.sl, datformat);
						var list = db.Database.SqlQuery<WarehouseDetail>(sql).ToList();
						list = list.Where(o => o.WarehouseNo != "US07").ToList();

						var fbascbcks = db.scbck.Where(o => o.countryid == "01" && o.realname.Contains("FBA")).ToList();
						var fbascbcksIDs = fbascbcks.Select(o => o.id).ToList();
						list = list.Where(o => !fbascbcksIDs.Contains(o.WarehouseNo)).ToList();

						Helper.Info(Order.number, "wms_api", JsonConvert.SerializeObject(list));

						#region 禁发restock
						var restockck = db.scbck.Where(o => o.countryid == "01" && o.realname.Contains("restock")).ToList();
						var restockIDs = restockck.Select(o => o.id).ToList();
						bool isContainRestock = list.Any(o => restockIDs.Contains(o.WarehouseNo));
						//禁发restock FDSUS,FDSCA,costway-giveaway,tiktok-giantex,tiktok-deal,tiktok-costzon,
						//包含 tiktok 都不行
						var Ban_RestockRule = MatchRules.FirstOrDefault(f => f.Type == "Ban_Restock");
						var forbiddenRestock_dianpus = db.scb_WMS_MatchRule_Details.Where(f => f.RID == Ban_Restock.ID && f.Type == "Store").Select(p => p.Parameter).ToList();
						//var forbiddenRestock_dianpus = new List<string>() { "fdsus", "fdsus-bp", "fdsca", "costway-giveaway", "petsjoy" };
						if (isContainRestock && (forbiddenRestock_dianpus.Contains(Order.dianpu.ToLower()) || CurrentItemNo.ToUpper().StartsWith("JL")))
						{
							list = list.Where(o => !restockIDs.Contains(o.WarehouseNo)).ToList();
							var message = $" {string.Join(",", forbiddenRestock_dianpus)},店铺名含tiktok的订单,货号JL开头的订单,禁发restock";
							Helper.Info(Order.number, "wms_api", message);
							if (list.Count == 0)
							{
								OrderMatch.Result.Message = message;
								return;
							}
						}
						//只有restock有货可发：重发，补发单+homedepot+fdsus-bp，进行提示，数据组手动派单即可
						//20240509 史倩文 fdsus-bp 禁发restock
						if (isContainRestock && (Order.xspt.ToLower() == "homedepot" || Order.temp3 == "8" || Order.temp3 == "4"))
						{
							list = list.Where(o => !restockIDs.Contains(o.WarehouseNo)).ToList();
							if (list.Count == 0)
							{
								var message = "重发,补发单,homedepot,只有restock有货可发,需手动派单";
								Helper.Info(Order.number, "wms_api", message);
								OrderMatch.Result.Message = message;
								return;
							}
						}
						#endregion

						if (Tocountry == "CA")
						{
							//if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday || DateTime.Now.DayOfWeek == DayOfWeek.Sunday || DateTime.Now.DayOfWeek == DayOfWeek.Monday)
							//{
							//    var startTime = DateTime.Now;
							//    var endTime = DateTime.Now;
							//    if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday)
							//    {
							//        startTime = DateTime.Now.Date;
							//        endTime = DateTime.Now.Date.AddDays(3);
							//    }
							//    if (DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
							//    {
							//        startTime = DateTime.Now.Date.AddDays(-1);
							//        endTime = DateTime.Now.Date.AddDays(2);
							//    }
							//    else if (DateTime.Now.DayOfWeek == DayOfWeek.Monday)
							//    {
							//        startTime = DateTime.Now.Date.AddDays(-2);
							//        endTime = DateTime.Now.Date.AddDays(1);
							//    }
							//    var qty1 = db.Database.SqlQuery<int>(string.Format($@"select isnull(SUM(s.sl),0) from scb_xsjl x inner join scb_xsjlsheet s on 
							//                x.number = s.father
							//                where country = 'US' and x.temp10='CA'
							//                and x.state = 0 and x.temp3=0 and filterflag > 5 AND x.newdateis>'{startTime.ToString("yyyy-MM-dd")}' AND x.newdateis<'{endTime.ToString("yyyy-MM-dd")}' AND x.hthm NOT LIKE 'Order%' AND x.hthm NOT LIKE 'return%'  and x.warehouseid in (141,173)")).ToList().FirstOrDefault();
							//    if (qty1 > 1600)
							//    {
							//        if (list.Any(c => c.WarehouseNo == "US21"))
							//        {
							//            list = list.Where(o => o.WarehouseNo != "US21").ToList();
							//            Helper.Info(Order.number, "wms_api", "US21，US121已超过1600，排除US21库存");
							//        }
							//        if (list.Any(c => c.WarehouseNo == "US121"))
							//        {
							//            list = list.Where(o => o.WarehouseNo != "US121").ToList();
							//            Helper.Info(Order.number, "wms_api", "US21，US121已超过1600，排除US121库存");
							//        }
							//    }
							//}


							if (DateTime.Now.ToString("yyyy-MM-dd").Equals("2023-05-09"))
							{

								var qty1 = db.Database.SqlQuery<int>(string.Format($@"select isnull(SUM(s.sl),0) from scb_xsjl x inner join scb_xsjlsheet s on 
                                            x.number = s.father
                                            where country = 'US' and x.temp10='CA'
                                            and x.state = 0 and x.temp3=0 and filterflag > 5 AND x.newdateis>'2023-05-09' AND x.newdateis<'2023-05-10' AND x.hthm NOT LIKE 'Order%' AND x.hthm NOT LIKE 'return%'  and x.warehouseid in (141)")).ToList().FirstOrDefault();
								if (qty1 > 400)
								{
									if (list.Any(c => c.WarehouseNo == "US21"))
									{
										list = list.Where(o => o.WarehouseNo != "US21").ToList();

										Helper.Info(Order.number, "wms_api", "US21已超过400");
									}
								}
							}
						}
						//只用fedex
						//20240424 张洁楠 US11只用fedex
						if (MatchRules.Any(f => f.Type == "Only_FedexGround_Send") && Tocountry == "CA")
						{
							var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_FedexGround_Send");
							var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
							if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == Order.dianpu.ToUpper()))
							{
								list = list.Where(o => o.WarehouseNo != "US17" && o.WarehouseNo != "US11").ToList();
							}
						}

						//temu-ca 只能发US11，US21
						if (Order.dianpu.ToLower() == "temu-ca")
						{
							list = list.Where(o => o.WarehouseNo == "US11" || o.WarehouseNo == "US21").ToList();
							if (list.Count == 0)
							{
								OrderMatch.Result.Message = "temu-ca 只能US11，US21发货";
								return;
							}
						}


						////20231103 wjf 当US10仓库当天打单满50单时,则当天不再用该仓库打单
						//if (list.Any(f => f.WarehouseNo == "US10"))
						//{
						//	var searchDate = DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd"));
						//	var US10Count = db.scb_xsjl.Where(p => p.state == 0 && p.warehouseid == 78 && p.newdateis >= searchDate && p.filterflag > 7 && p.temp3 != "7" && p.temp6 != "12").Count();
						//	if (US10Count > 50)
						//	{
						//		list = list.Where(p => p.WarehouseNo != "US10").ToList();
						//		Helper.Info(Order.number, "wms_api", "US10仓库当天打单满50单时,不用US10打单了");
						//	}
						//}


						#region CanCAToUS
						if (Tocountry == "US" && !OnlyUPS && CanSendCAWarehouseMatchRule_Details.Any(o => o == "US21") && list.Any(f => f.WarehouseNo == "US21"))
						{
							var forbiddenCA_match = MatchRules.Where(p => p.Type == "Ban_FedexToUS").FirstOrDefault();
							var forbiddenCA_matchDetails = db.scb_WMS_MatchRule_Details.Where(p => p.RID == forbiddenCA_match.ID).ToList();
							var forbiddenCA_xspts = forbiddenCA_matchDetails.Where(p => p.Type == "platform").ToList(); //new List<string>() { "tiktok", "vendor", "temu", "shein", "target", "walmart" }; //, "tiktok-giantex", "tiktok-deal", "tiktok-costzon"
																														//var USNoStockToCA_xspts = new List<string>() { "temu", "shein", "target", "walmart" };
							var forbiddenCA_dianpus = forbiddenCA_matchDetails.Where(p => p.Type == "Store").ToList();
							var matchcks = db.scbck.Where(p => p.countryid == "01" && p.IsMatching == 1 && p.state == 1 && (p.wh_type == "" || p.wh_type == "restock")).Select(p => p.id).ToList();
							list = list.Where(p => p.WarehouseNo.Contains("US") && matchcks.Contains(p.WarehouseNo)).ToList();
							if (forbiddenCA_xspts.Any(p => p.Parameter.ToLower() == Order.xspt.ToLower()))
							{
								list = list.Where(p => p.WarehouseNo != "US21").ToList();
								if (list.Count == 0)
								{
									OrderMatch.Result.Message = $"匹配失败！{Order.xspt} 平台不支持CA发US";
									return;
								}
							}
							else if (forbiddenCA_dianpus.Any(p => p.Parameter.ToLower() == Order.dianpu.ToLower()))
							{
								list = list.Where(p => p.WarehouseNo != "US21").ToList();
								if (list.Count == 0)
								{
									OrderMatch.Result.Message = $"匹配失败！{Order.dianpu} 店铺不支持CA发US";
									return;
								}
							}
							else if (AnyNoCanSendCAGood)
							{
								list = list.Where(p => p.WarehouseNo != "US21" && p.WarehouseNo != "US121").ToList();
								if (list.Count == 0)
								{
									OrderMatch.Result.Message = $"匹配失败！{sheets2.First().cpbh} 属于调拨货号，不支持CA发US";
									return;
								}
							}
							//else if (USNoStockToCA_xspts.Any(p => p.ToLower() == Order.xspt.ToLower()) && list.Any(p => p.WarehouseNo != "US21"))
							//{
							//	CanCAToUS = false;
							//}
							else
							{
								CanCAToUS = true;
							}
						}
						#endregion

						if (CanCAToUS)
						{
							if (list.Any(f => f.WarehouseNo == "US21") && CanSendCAWarehouseMatchRule_Details.Any(o => o == "US21"))
							{
								var exceedQtyLimit = false;

								if (list.Any(p => p.WarehouseNo != "US21" && p.WarehouseNo.Contains("US")))
								{
									var limitRule = MatchRules.FirstOrDefault(p => p.Type == "US21_FedexToUS");
									var limit = db.scb_WMS_MatchRule_Details.Where(p => p.RID == limitRule.ID && p.Type == "limit").First();
									if (!string.IsNullOrEmpty(limit.Parameter))
									{
										var limetQty = int.Parse(limit.Parameter);
										var dat = DateTime.Now;
										var count = db.Database.SqlQuery<int>(string.Format($@"select isnull(SUM(x.zsl),0) from scb_xsjl x 
				            where country = 'US' and x.temp10 = 'US'
				            and x.state = 0 and filterflag > 5 AND x.newdateis>'{dat.ToString("yyyy-MM-dd")}' AND x.newdateis<'{dat.AddDays(1).ToString("yyyy-MM-dd")}'
							AND x.hthm NOT LIKE 'Order%' AND x.hthm NOT LIKE 'return%'AND x.logicswayoid =79 and x.warehouseid =141")).FirstOrDefault();
										if (count > limetQty)
										{
											exceedQtyLimit = true;
											CanCAToUS = false;
										}
									}
								}
								var tt = Warehouses.FirstOrDefault(c => c.id == "US21");
								if (tt != null && !exceedQtyLimit)
								{
									CanCAToUS = true;
									tt.Sort = 10099;
									lcks.Add(tt);
								}

							}
							if (!CanSendCAWarehouseMatchRule_Details.Any(o => o == "US21"))
							{
								list = list.Where(o => o.WarehouseNo != "US21").ToList();
							}
						}


						#region 美国没货，需要从加拿大发货  //20240530 武金凤  关闭之前用部分sku和覆盖部分美国地区的规则并启用以下新的匹配规则  注释了
						//var NoStockCAToUSGood = MatchRules.FirstOrDefault(f => f.Type == "NoStockCAToUSGood");
						//var NoStockCAToUSGood_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == NoStockCAToUSGood.ID).Select(o => o.Parameter).ToList();

						//var AnyNoStockCAToUSGood = sheets2.Where(o => NoStockCAToUSGood_Details.Contains(o.cpbh)).Any();

						////, "tiktok-giantex", "tiktok-deal", "tiktok-costzon"  包含tiktok 就不行
						////20240418 wjf temu禁发FedExTOUS，不能发加拿大仓
						//var forbiddenCA_dianpus = new List<string>() { "walmart" };
						//if (AnyNoStockCAToUSGood && list.Any(f => f.WarehouseNo == "US21"))
						//{
						//	if ((Order.xspt.ToLower() != "shein") && (Order.xspt.ToLower() != "target") && (Order.xspt.ToLower() != "temu") && (Order.xspt.ToLower() != "walmart") && !forbiddenCA_dianpus.Contains(Order.dianpu.ToLower()) && !Order.dianpu.ToLower().Contains("tiktok"))
						//	{
						//		var tt = Warehouses.FirstOrDefault(c => c.id == "US21");
						//		if (tt != null)
						//		{
						//			CanCAToUS = true;
						//			NoStrockUSFromCA = true;
						//			tt.Sort = 10099;
						//			lcks.Add(tt);
						//		}
						//	}
						//	else if (Tocountry == "US" && !CanCAToUS && list.Any(f => f.WarehouseNo == "US21") && list.Where(p => p.WarehouseNo.Contains("US")).Count() == 1)
						//	{
						//		OrderMatch.Result.Message = $"匹配失败,库存不足！US21有货，{sheets2.First().cpbh} 货号{(AnyNoStockCAToUSGood ? "可以" : "不可以")}从CA发,temu、shein、target、walmart、tickok平台不发";
						//		return;
						//	}
						//}



						////if (AnyNoStockCAToUSGood && (Order.xspt.ToLower() != "shein") && (Order.xspt.ToLower() != "target") && (Order.xspt.ToLower() != "temu") && (Order.xspt.ToLower() != "walmart") && !forbiddenCA_dianpus.Contains(Order.dianpu.ToLower()) && !Order.dianpu.ToLower().Contains("tiktok"))
						////{
						////	if (list.Any(f => f.WarehouseNo == "US21"))
						////	{
						////		var tt = Warehouses.FirstOrDefault(c => c.id == "US21");
						////		if (tt != null)
						////		{
						////			CanCAToUS = true;
						////			NoStrockUSFromCA = true;
						////			tt.Sort = 10099;
						////			lcks.Add(tt);
						////		}

						////	}
						////}
						////else if (Tocountry == "US" && !CanCAToUS && list.Any(f => f.WarehouseNo == "US21") && list.Where(p => p.WarehouseNo.Contains("US")).Count() == 1)
						////{
						////	OrderMatch.Result.Message = $"匹配失败,库存不足！US21有货，{sheets2.First().cpbh} 货号{(AnyNoStockCAToUSGood ? "可以" : "不可以")}从CA发,temu、shein、target、walmart、tickok平台不发";
						////	return;
						////}
						#endregion

						//2022-10-28 没啥用，注释了
						//fds库存锁定
						//if (list.Any(f => f.FDS_StockItemNo > 0) && Order.temp10 == "US")
						//{
						//    if (list.Count(f => f.FDS_StockItemNo > 0 && f.EffectiveInventory > f.FDS_Stock && f.FDS_Stock > 0) != list.Count &&
						//        list.Any(f => f.FDS_StockItemNo > 0 && f.EffectiveInventory > f.FDS_Stock && f.FDS_Stock > 0))
						//        list.RemoveAll(f => f.FDS_StockItemNo > 0 && f.EffectiveInventory <= f.FDS_Stock && f.FDS_Stock > 0);//去掉库存保障
						//}

						////记录下有效库存多少用于后面pioneer锁定库存
						//var Effectives = new List<ScbckModel>();
						//foreach (var f in lcks)
						//{
						//    if (list.Any(g => g.WarehouseNo == f.id))
						//    {
						//        f.EffectiveInventory = list.First(g => g.WarehouseNo == f.id).EffectiveInventory;
						//        Effectives.Add(f);
						//    }
						//}
						//var T1 = list.ToList();
						//if (Tocountry == "US")
						//{
						//    T1 = T1.Where(o => o.WarehouseNo != "US21" || o.WarehouseNo != "US21").ToList();
						//}
						//IsExsitStockWarehouse = T1.Exists(o => o.WarehouseNo.Contains("US"));

						ws.Add(item.number, lcks.Where(f => list.Any(g => g.WarehouseNo == f.id)).ToList());

					}

					#endregion
				}
			}
			#endregion

			//初始化
			var Logisticsmodes = db.Database.SqlQuery<LogisticsWareRelationModel>("select oid logisticsId,operatoren logisticsName, warehoues  warehouseIds  from scb_Logisticsmode where IsEnable = 1 and warehoues like '%US%'").ToList(); //db.scb_Logisticsmode.Where(f => f.warehoues.Contains("US") && f.IsEnable == 1).ToList();
			var EffectiveWarehouse = new List<ScbckModel>();
			var IsExistBySerice = !string.IsNullOrEmpty(OrderMatch.SericeType);//后续走尺寸重量标准判断物流

			#region 获取有效仓库
			if (ws.Any())
			{
				if (ws.Count() == 1)
				{
					if (ws.FirstOrDefault().Value.Any())
					{
						EffectiveWarehouse = ws.FirstOrDefault().Value;
					}
					else
					{
						#region 推荐替换货号   
						//var goods = sheets2.FirstOrDefault().cpbh;
						//var goodssheet = db.N_goodsSheet.Where(f => f.ItemNo1 == goods && (f.Country == "US" || f.Country == "CA")).ToList();
						//if (goodssheet.Any())
						//{
						//    TempKcslHelper tempKcslHelper = new TempKcslHelper();
						//    var items = string.Empty;
						//    foreach (var item in goodssheet.OrderBy(f => f.Sort))
						//    { 
						//        //var kcjl = db.scb_kcjl_area.Where(f => f.cpbh == item.ItemNo2 && f.place.Contains("US")).ToList();
						//        //if (kcjl.Any())
						//        //{
						//        //    items += string.Format("{0}({1})({2}),", item.ItemNo2, kcjl.OrderBy(f => f.place).FirstOrDefault().place, item.Country);
						//        //}
						//        //else
						//        //{
						//        //    items += item.ItemNo2 + ",";
						//        //} 
						//        if (tempKcslHelper.IsGoodsEnough(item.ItemNo2, "US", DateTime.Now.ToString("yyyy-MM-dd"), 0))
						//        {
						//            items += item.ItemNo2 + ",";
						//        }
						//    }

						//    OrderMatch.Result.Message += string.Format(" 推荐更换货号:{0}", items.TrimEnd(','));
						//    return;
						//}

						string oldCpbh = sheets2.FirstOrDefault().cpbh;
						string newCpbh = RecommendCpbh(Order.temp10, Order.dianpu, oldCpbh, sheets2.FirstOrDefault().sl, Order.country, Order.xspt);
						if (oldCpbh != newCpbh)
						{
							OrderMatch.Result.Message += string.Format(" 推荐更换货号:{0}", newCpbh.TrimEnd(','));
							return;
						}
						#endregion
					}
				}
				else
				{
					var Baselist = ws.FirstOrDefault().Value;
					foreach (var item in ws)
					{
						Baselist = Baselist.Where(f => item.Value.Any(g => g.id == f.id)).ToList();
					}
					if (Baselist.Any())
					{
						EffectiveWarehouse = Baselist;
					}
				}
			}

			if (!EffectiveWarehouse.Any())
			{
				//if (IsExsitStockWarehouse)
				//{
				//    OrderMatch.Result.Message = "最优仓无法配送，其他仓有货";
				//    return;
				//}
				//else
				//{
				OrderMatch.Result.Message = "匹配错误,库存不足";
				return;
				//}
			}
			else
			{
				//输出有效仓库
				//LogHelper.WriteLog(JsonConvert.SerializeObject(EffectiveWarehouse));

				//已指定物流
				if (IsExistBySerice)
				{
					//按照物流过滤有效仓库
					var oid2 = OrderMatch.Logicswayoid;
					var EffectiveWarehouse2 = EffectiveWarehouse.Where(f => f.oids.Contains("," + oid2 + ",")).ToList();
					if (EffectiveWarehouse2.Any())
					{
						//2022-11-30 ljh调整
						//if (IsInternational)
						//{

						//}
						//else //排除国际件
						//{
						//    #region 仓库顺序放到最后 
						//    var Last_Warehouses = MatchRules.FirstOrDefault(f => f.Type == "Last_Warehouses");
						//    if (Last_Warehouses != null)
						//    {
						//        var warehouses = Last_Warehouses.Warehouse.Split(',');
						//        foreach (var item in warehouses)
						//        {
						//            var last = EffectiveWarehouse2.FirstOrDefault(f => f.id == item);
						//            if (last != null)
						//            {
						//                EffectiveWarehouse2.Remove(last);
						//                EffectiveWarehouse2.Add(last);
						//            }
						//        }
						//    }
						//    #endregion
						//}
						var CurrentWarehouse2 = EffectiveWarehouse2.First();
						OrderMatch.WarehouseID = CurrentWarehouse2.number;
						OrderMatch.WarehouseName = CurrentWarehouse2.id;
						//国际件发货需要判断ups or fedex
						if (IsInternational)
						{
							if (CurrentWarehouse2.Origin == "CA")
								IsExistBySerice = false;
							else
							{
								OrderMatch.Logicswayoid = 48;
								OrderMatch.SericeType = "UPSInternational";
							}
						}
					}
					else
					{
						//仓库唯一时直接发指定物流
						if (EffectiveWarehouse.Count == 1)
						{
							IsExistBySerice = true;
							OrderMatch.WarehouseID = EffectiveWarehouse.First().number;
							OrderMatch.WarehouseName = EffectiveWarehouse.First().id;
						}
						else
						{
							OrderMatch.Result.Message = string.Format("匹配错误,指定物流({0})无符合仓库可以发货", OrderMatch.SericeType);
							return;
						}
					}
				}

			}

			#endregion

			#region 匹配适合的物流
			if (IsExistBySerice && OrderMatch.WarehouseID != 0)
			{
				if (CanCAToUS && OrderMatch.Logicswayoid == 18 && (OrderMatch.WarehouseName == "US21" || OrderMatch.WarehouseName == "US121") && Tocountry == "US")
				{
					OrderMatch.Logicswayoid = 79;
					OrderMatch.SericeType = "FedexToUS";
				}

				return;//仓库和物流已匹配
			}
			else
			{
				#region 寻找物流
				bool IsFind2 = false;
				bool IsAMZN = false;
				var configs = db.scb_WMS_LogisticsForWeight.Where(f => f.IsChecked == 1).ToList();

				#region 有优先级高的重新排序
				var levels = db.scb_WMS_UnshippedTotal.Where(f => f.Level > 0).ToList();
				if (levels.Any())
				{
					WarehouseLevel(EffectiveWarehouse.First(), _UPSSkuList, configs, MatchRules, Order, sheets,
					   weight, weight_g, volNum, volNum2, weight_vol, weight_phy, maxNum, secNum, Tocountry, CanCAToUS, ref OrderMatch, IsAMZN);

					var currentAreaLocation = EffectiveWarehouse.First().AreaLocation;
					var AreaLocation = EffectiveWarehouse.Where(f => f.AreaLocation == currentAreaLocation).ToList();
					var noAreaLocation = EffectiveWarehouse.Where(f => f.AreaLocation != currentAreaLocation).ToList();

					if (OrderMatch.Logicswayoid == 18 && levels.Any(f => f.Way == "Fedex"))
					{

						foreach (var item in levels.Where(f => f.Way == "Fedex").OrderBy(f => f.Level))
						{
							var w = AreaLocation.Where(f => f.id == item.Warehouse);
							if (w.Any())
							{
								var w2 = w.First();
								w2.Levels = item.Level;
							}
						}
					}
					if ("3,4,24".Split(',').Contains(OrderMatch.Logicswayoid.ToString()) && levels.Any(f => f.Way == "UPS"))
					{
						foreach (var item in levels.Where(f => f.Way == "UPS").OrderBy(f => f.Level))
						{
							var w = EffectiveWarehouse.Where(f => f.id == item.Warehouse);
							if (w.Any())
							{
								var w2 = w.First();
								w2.Levels = item.Level;
							}
						}
					}

					EffectiveWarehouse.Clear();
					EffectiveWarehouse.AddRange(AreaLocation.OrderBy(o => o.Levels).ThenBy(f => f.Sort));
					EffectiveWarehouse.AddRange(noAreaLocation.OrderBy(o => o.Levels).ThenBy(f => f.Sort));

					//输出限制后有效仓库
					//LogHelper.WriteLog("Level:" + JsonConvert.SerializeObject(EffectiveWarehouse));
				}
				//if (CanCAToUS && EffectiveWarehouse.Any(f => f.id == "US21" || f.id == "US121"))
				//{
				//	var t1list = EffectiveWarehouse.Where(f => f.id == "US21" || f.id == "US121").OrderBy(o => o.Sort).ToList();
				//	var t2list = EffectiveWarehouse.Where(f => f.id != "US21" && f.id != "US121").ToList();

				//	EffectiveWarehouse.Clear();
				//	EffectiveWarehouse.AddRange(t1list);
				//	EffectiveWarehouse.AddRange(t2list);
				//}
				#endregion


				//2022-11-30 ljh调整
				//if (IsInternational)
				//{

				//}
				//else //排除国际件
				//{
				//    #region 仓库顺序放到最后
				//    var Last_Warehouses = MatchRules.FirstOrDefault(f => f.Type == "Last_Warehouses");
				//    if (Last_Warehouses != null)
				//    {
				//        var warehouses = Last_Warehouses.Warehouse.Split(',');
				//        foreach (var item in warehouses)
				//        {
				//            var last = EffectiveWarehouse.FirstOrDefault(f => f.id == item);
				//            if (last != null && OrderMatch.SericeType != "UPSInternational")
				//            {
				//                EffectiveWarehouse.Remove(last);
				//                EffectiveWarehouse.Add(last);
				//            }
				//        }
				//    }
				//    #endregion
				//}

				#region pioneer留库存发货 每个区域低于10个后保留
				//var Pioneer_LockInventory = MatchRules.FirstOrDefault(f => f.Type == "Pioneer_LockInventory");
				//if (Pioneer_LockInventory != null)
				//{
				//    //涉及产品编号清单
				//    var Pioneer_LockInventorys = db.scb_WMS_MatchRule_Details.Where(f => f.RID == Pioneer_LockInventory.ID).ToList();
				//    if (sheets.Any(f => Pioneer_LockInventorys.Any(g => g.Parameter == f.cpbh)))
				//    {

				//    }
				//}
				#endregion

				var BeforeModificationLogicswayoid = OrderMatch.Logicswayoid;
				var BeforeModificationSericeType = OrderMatch.SericeType;

				#region 仓库匹配物流(最后通过物流来判断仓库是否能发)

				IsFind2 = MatchActualWarehouseUS(Logisticsmodes, _UPSSkuList, cpRealSizeinfos, Order, configs, MatchRules, sheets, weightReal, weight, weight_g, volNum, volNum2, weight_vol, weight_phy, maxNum, secNum, Tocountry, CanCAToUS, ref OrderMatch, ref EffectiveWarehouse, true, ignoreAMZN: ignoreAMZN, aircondition_box: aircondition_box); //true 因为传到下面的EffectiveWarehouse 的有效库存字段数据不完整，所以暂时不能用wms接口，还要改																																															 //因为AMZN优先发货，所以加个标记位记一下
				IsAMZN = OrderMatch.Logicswayoid == 114 && !ignoreAMZN;//因为AMZN优先发货，所以加个标记位记一下匹配结果

				//foreach (var item in EffectiveWarehouse)
				//{
				//	//var Warehouse = item.id;
				//	WarehouseLevel(item, configs, MatchRules, sheets,
				//		weight, weight_g, volNum, volNum2, weight_vol, weight_phy, maxNum, secNum, Tocountry, CanCAToUS, ref OrderMatch);

				//	if (item.oids.Contains("," + OrderMatch.Logicswayoid + ","))
				//	{
				//		if (!NeedWMSStock && item.EffectiveInventory < 3)
				//		{
				//			NeedWMSStock = true;
				//			//获取wms库存
				//			//回刷EffectiveWarehouse
				//			GetWMSStock = true;
				//			break;

				//		}
				//		if (!GetWMSStock)
				//		{
				//			IsFind2 = true;
				//			OrderMatch.WarehouseID = item.number;
				//			OrderMatch.WarehouseName = item.id;
				//			break;
				//		}
				//	}
				//}

				#region wjw的一些特殊要求
				//tangavendor这个店铺的订单匹配仓库的优先级进行调整，US11放在最后
				if (Order.dianpu.ToLower().Equals("tangavendor"))
				{
					if (EffectiveWarehouse.Where(o => o.id == "US11").Any())
					{
						var last = EffectiveWarehouse.FirstOrDefault(f => f.id == "US11");
						if (last != null)
						{
							EffectiveWarehouse.Remove(last);
							EffectiveWarehouse.Add(last);
						}
					}
				}

				////2022-12-19 wjf,US10的优先级 放到US06 US16 的最上面
				////20231127 wjf US10 > US16,US06 取消
				//if (OrderMatch.WarehouseID == 99 || OrderMatch.WarehouseID == 24)
				//{
				//	//EffectiveWarehouse为有库存的仓库列表
				//	var EffectiveWarehouse2 = EffectiveWarehouse.Where(o => o.number == 78).OrderByDescending(o => o.OriginSort).ToList();
				//	if (EffectiveWarehouse2.Any())
				//	{
				//		Helper.Info(Order.number, "wms_api", $"{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID} 去替换仓库(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType})");
				//		OrderMatch.Logicswayoid = BeforeModificationLogicswayoid;
				//		OrderMatch.SericeType = BeforeModificationSericeType;
				//		foreach (var item in EffectiveWarehouse2)
				//		{
				//			//var Warehouse = item.id;
				//			WarehouseLevel(item, configs, MatchRules, sheets,
				//				weight, weight_g, volNum, volNum2, weight_vol, weight_phy, maxNum, secNum, Tocountry, CanCAToUS, ref OrderMatch);

				//			if (item.oids.Contains("," + OrderMatch.Logicswayoid + ","))
				//			{
				//				IsFind2 = true;
				//				OrderMatch.WarehouseID = item.number;
				//				OrderMatch.WarehouseName = item.id;
				//				Helper.Info(Order.number, "wms_api", $"仓库替换为{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID}(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType}");
				//				break;
				//			}
				//		}
				//	}
				//}


				//2022-04-12wjw要求当匹配到US10，US12，US17，在US06，US16有货的前提下，替换当前仓库为US06，US16，其中US06优先级高于US16
				//判断仓库判断仓库后，在进行EffectiveWarehouse挑出US06和US16进行二次判断
				//2022-05-19 取消us12和us10  优先级
				//2022-11-16 调整了us06和us16优先级，uS16高于US06
				if (OrderMatch.WarehouseID == 116)
				{
					//EffectiveWarehouse为有库存的仓库列表
					var EffectiveWarehouse2 = EffectiveWarehouse.Where(o => o.number == 99 || o.number == 24).OrderByDescending(o => o.OriginSort).ToList();
					if (EffectiveWarehouse2.Any())
					{
						Helper.Info(Order.number, "wms_api", $"{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID} 去替换仓库(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType})");
						OrderMatch.Logicswayoid = BeforeModificationLogicswayoid;
						OrderMatch.SericeType = BeforeModificationSericeType;
						foreach (var item in EffectiveWarehouse2)
						{
							//var Warehouse = item.id;
							WarehouseLevel(item, _UPSSkuList, configs, MatchRules, Order, sheets,
								weight, weight_g, volNum, volNum2, weight_vol, weight_phy, maxNum, secNum, Tocountry, CanCAToUS, ref OrderMatch, IsAMZN);

							if (item.oids.Contains("," + OrderMatch.Logicswayoid + ","))
							{
								IsFind2 = true;
								OrderMatch.WarehouseID = item.number;
								OrderMatch.WarehouseName = item.id;
								Helper.Info(Order.number, "wms_api", $"仓库替换为{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID}(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType}");
								break;
							}
						}
					}
				}


				//US12 Houston仓的发货优先级下调至US10Dallas
				//20231102 武金凤 US10 Dallas仓库计划于当地时间3号恢复一次拣货，US10 Dallas仓的发货优先级下调至US12 Houston仓库下面
				//20231107 武金凤 请将美国US10 Dallas仓库的优先级调为US10＞US12。
				if (OrderMatch.WarehouseID == 93)
				{
					//EffectiveWarehouse为有库存的仓库列表
					var EffectiveWarehouse2 = EffectiveWarehouse.Where(o => o.number == 78).OrderBy(o => o.OriginSort).ToList();
					if (EffectiveWarehouse2.Any())
					{
						Helper.Info(Order.number, "wms_api", $"{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID} 去替换仓库(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType})");
						OrderMatch.Logicswayoid = BeforeModificationLogicswayoid;
						OrderMatch.SericeType = BeforeModificationSericeType;
						foreach (var item in EffectiveWarehouse2)
						{
							//var Warehouse = item.id;
							WarehouseLevel(item, _UPSSkuList, configs, MatchRules, Order, sheets,
								weight, weight_g, volNum, volNum2, weight_vol, weight_phy, maxNum, secNum, Tocountry, CanCAToUS, ref OrderMatch, IsAMZN);

							if (item.oids.Contains("," + OrderMatch.Logicswayoid + ","))
							{
								IsFind2 = true;
								OrderMatch.WarehouseID = item.number;
								OrderMatch.WarehouseName = item.id;
								Helper.Info(Order.number, "wms_api", $"仓库替换为{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID}(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType}");
								break;
							}
						}
					}
				}


				//优先级：US21 QQ＞US04 Dayton＞US11 Borden；
				if (CanCAToUS && (OrderMatch.WarehouseID == 82 || OrderMatch.WarehouseID == 17))
				{
					var EffectiveWarehouse2 = EffectiveWarehouse.Where(p => p.id == "US21").ToList();
					if (EffectiveWarehouse2.Any())
					{
						Helper.Info(Order.number, "wms_api", $"{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID} 去替换仓库(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType})");
						OrderMatch.Logicswayoid = BeforeModificationLogicswayoid;
						OrderMatch.SericeType = BeforeModificationSericeType;
						foreach (var item in EffectiveWarehouse2)
						{
							//var Warehouse = item.id;
							WarehouseLevel(item, _UPSSkuList, configs, MatchRules, Order, sheets,
								weight, weight_g, volNum, volNum2, weight_vol, weight_phy, maxNum, secNum, Tocountry, CanCAToUS, ref OrderMatch, IsAMZN);

							if (item.oids.Contains("," + OrderMatch.Logicswayoid + ","))
							{
								IsFind2 = true;
								OrderMatch.WarehouseID = item.number;
								OrderMatch.WarehouseName = item.id;
								Helper.Info(Order.number, "wms_api", $"仓库替换为{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID}(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType}");
								break;
							}
						}
					}
				}

				//US04Dayton的优先级调到高于US11Borden
				//US04，08，09的优先级调到高于US11Borden
				//US04>US08>US09
				//2023-03-07,去掉US09换us11，US04，08的优先级调到高于US11Borden
				//2023-03-09,去掉US09换us08，US04的优先级调到高于US11Borden
				//之前的需求
				//2023-04-03起北京时间周二到周五（包含周五）为US04＞US11，即当仓库为US11时用US04替换
				//2023-04-03起周六到周一（包含周一）为US04＞US08>US11，即当仓库为US11时用US04，US08替换，US04优先级比US08高
				//2023-04-15 为US04＞US08>US09>US11
				//2023-11-27 武金凤 都改成 US08 Chicago＞US04 Dayton>US11 Borden
				var dw = DateTime.Now.DayOfWeek.ToString("d");
				#region US11替换
				if (OrderMatch.WarehouseID == 82)
				{

					//EffectiveWarehouse为有库存的仓库列表
					var EffectiveWarehouse2 = EffectiveWarehouse.Where(o => o.number == 17 || o.number == 44).OrderByDescending(o => o.number).ToList();
					if (EffectiveWarehouse2.Any())
					{
						Helper.Info(Order.number, "wms_api", $"{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID} 去替换仓库(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType})");
						OrderMatch.Logicswayoid = BeforeModificationLogicswayoid;
						OrderMatch.SericeType = BeforeModificationSericeType;
						foreach (var item in EffectiveWarehouse2)
						{
							//var Warehouse = item.id;
							WarehouseLevel(item, _UPSSkuList, configs, MatchRules, Order, sheets,
								weight, weight_g, volNum, volNum2, weight_vol, weight_phy, maxNum, secNum, Tocountry, CanCAToUS, ref OrderMatch, IsAMZN);

							if (item.oids.Contains("," + OrderMatch.Logicswayoid + ","))
							{
								IsFind2 = true;
								OrderMatch.WarehouseID = item.number;
								OrderMatch.WarehouseName = item.id;
								Helper.Info(Order.number, "wms_api", $"仓库替换为{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID}(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType}");
								break;
							}
						}
					}

					//if (dw == "1")
					//{
					//	//EffectiveWarehouse为有库存的仓库列表
					//	var EffectiveWarehouse2 = EffectiveWarehouse.Where(o => o.number == 17 || o.number == 44).OrderBy(o => o.OriginSort).ToList();
					//	if (EffectiveWarehouse2.Any())
					//	{
					//		Helper.Info(Order.number, "wms_api", $"{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID} 去替换仓库(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType})");
					//		OrderMatch.Logicswayoid = BeforeModificationLogicswayoid;
					//		OrderMatch.SericeType = BeforeModificationSericeType;
					//		foreach (var item in EffectiveWarehouse2)
					//		{
					//			//var Warehouse = item.id;
					//			WarehouseLevel(item, configs, MatchRules, sheets,
					//				weight, weight_g, volNum, volNum2, weight_vol, weight_phy, maxNum, secNum, Tocountry, CanCAToUS, ref OrderMatch);

					//			if (item.oids.Contains("," + OrderMatch.Logicswayoid + ","))
					//			{
					//				IsFind2 = true;
					//				OrderMatch.WarehouseID = item.number;
					//				OrderMatch.WarehouseName = item.id;
					//				Helper.Info(Order.number, "wms_api", $"仓库替换为{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID}(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType}");
					//				break;
					//			}
					//		}
					//	}
					//}
					//else
					//{
					//	var EffectiveWarehouse2 = EffectiveWarehouse.Where(o => o.number == 17).OrderBy(o => o.OriginSort).ToList();
					//	if (EffectiveWarehouse2.Any())
					//	{
					//		Helper.Info(Order.number, "wms_api", $"{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID} 去替换仓库(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType})");
					//		OrderMatch.Logicswayoid = BeforeModificationLogicswayoid;
					//		OrderMatch.SericeType = BeforeModificationSericeType;
					//		foreach (var item in EffectiveWarehouse2)
					//		{
					//			//var Warehouse = item.id;
					//			WarehouseLevel(item, configs, MatchRules, sheets,
					//				weight, weight_g, volNum, volNum2, weight_vol, weight_phy, maxNum, secNum, Tocountry, CanCAToUS, ref OrderMatch);

					//			if (item.oids.Contains("," + OrderMatch.Logicswayoid + ","))
					//			{
					//				IsFind2 = true;
					//				OrderMatch.WarehouseID = item.number;
					//				OrderMatch.WarehouseName = item.id;
					//				Helper.Info(Order.number, "wms_api", $"仓库替换为{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID}(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType}");
					//				break;
					//			}
					//		}
					//	}
					//}
				}
				//if ("6,0,1".Contains(dw) && OrderMatch.WarehouseID == 82)
				//{
				//    //EffectiveWarehouse为有库存的仓库列表
				//    var EffectiveWarehouse2 = EffectiveWarehouse.Where(o => o.number == 17).OrderBy(o => o.OriginSort).ToList();
				//    if (EffectiveWarehouse2.Any())
				//    {
				//        Helper.Info(Order.number, "wms_api", $"{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID} 去替换仓库(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType})");
				//        OrderMatch.Logicswayoid = BeforeModificationLogicswayoid;
				//        OrderMatch.SericeType = BeforeModificationSericeType;
				//        foreach (var item in EffectiveWarehouse2)
				//        {
				//            //var Warehouse = item.id;
				//            WarehouseLevel(item, configs, MatchRules, sheets,
				//                weight, weight_g, volNum, volNum2, weight_vol, weight_phy, maxNum, secNum, Tocountry, CanCAToUS, ref OrderMatch);

				//            if (item.oids.Contains("," + OrderMatch.Logicswayoid + ","))
				//            {
				//                IsFind2 = true;
				//                OrderMatch.WarehouseID = item.number;
				//                OrderMatch.WarehouseName = item.id;
				//                Helper.Info(Order.number, "wms_api", $"仓库替换为{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID}(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType}");
				//                break;
				//            }
				//        }
				//    }
				//}
				//if ("6,0,1".Contains(dw) && OrderMatch.WarehouseID == 82)
				//{
				//    //EffectiveWarehouse为有库存的仓库列表
				//    var EffectiveWarehouse2 = EffectiveWarehouse.Where(o => o.number == 17 || o.number == 44).OrderBy(o => o.OriginSort).ToList();
				//    if (EffectiveWarehouse2.Any())
				//    {
				//        Helper.Info(Order.number, "wms_api", $"{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID} 去替换仓库(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType})");
				//        OrderMatch.Logicswayoid = BeforeModificationLogicswayoid;
				//        OrderMatch.SericeType = BeforeModificationSericeType;
				//        foreach (var item in EffectiveWarehouse2)
				//        {
				//            //var Warehouse = item.id;
				//            WarehouseLevel(item, configs, MatchRules, sheets,
				//                weight, weight_g, volNum, volNum2, weight_vol, weight_phy, maxNum, secNum, Tocountry, CanCAToUS, ref OrderMatch);

				//            if (item.oids.Contains("," + OrderMatch.Logicswayoid + ","))
				//            {
				//                IsFind2 = true;
				//                OrderMatch.WarehouseID = item.number;
				//                OrderMatch.WarehouseName = item.id;
				//                Helper.Info(Order.number, "wms_api", $"仓库替换为{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID}(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType}");
				//                break;
				//            }
				//        }
				//    }
				//}
				#endregion


				////US16Redlands的优先级调到高于US06Fontana
				//if (OrderMatch.WarehouseID == 24)
				//{
				//    //EffectiveWarehouse为有库存的仓库列表
				//    var EffectiveWarehouse2 = EffectiveWarehouse.Where(o => o.number == 99).OrderBy(o => o.OriginSort).ToList();
				//    if (EffectiveWarehouse2.Any())
				//    {
				//        Helper.Info(Order.number, "wms_api", $"{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID} 去替换仓库(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType})");
				//        OrderMatch.Logicswayoid = BeforeModificationLogicswayoid;
				//        OrderMatch.SericeType = BeforeModificationSericeType;
				//        foreach (var item in EffectiveWarehouse2)
				//        {
				//            //var Warehouse = item.id;
				//            WarehouseLevel(item, configs, MatchRules, sheets,
				//                weight, weight_g, volNum, volNum2, weight_vol, weight_phy, maxNum, secNum, Tocountry, CanCAToUS, ref OrderMatch);

				//            if (item.oids.Contains("," + OrderMatch.Logicswayoid + ","))
				//            {
				//                IsFind2 = true;
				//                OrderMatch.WarehouseID = item.number;
				//                OrderMatch.WarehouseName = item.id;
				//                Helper.Info(Order.number, "wms_api", $"仓库替换为{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID}(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType}");
				//                break;
				//            }
				//        }
				//    }
				//}



				#region 20240822 武金凤  二次拣货物流： 二次发货的时间阶段，UPS关掉优先改发Amazon shipping，其他改发FedEx。   注释 20240823
				//var us_dat = AMESTime.AMESNowUSA;//美西时间
				//var second_westWarehouseList = new List<string>() { "US06", "US16", "US17" };
				//var second_eastWarehouseList = new List<string>() { "US09", "US11" };

				//var match_WarehouseName = OrderMatch.WarehouseName;

				//if (!string.IsNullOrEmpty(match_WarehouseName) && OrderMatch.Logicswayoid == 4 && Order.temp10 == "US" && (Order.xspt == "AM" || Order.xspt == "AMAZON"))
				//{
				//	var secondstartTime = new DateTime(2024, 8, 22, 0, 0, 0, 0);
				//	var west_cutofftime = new DateTime(us_dat.Year, us_dat.Month, us_dat.Day, 14, 10, 0, 0);
				//	var west_starttime = new DateTime(us_dat.Year, us_dat.Month, us_dat.Day, 3, 20, 0, 0);
				//	if (match_WarehouseName == "US06")
				//	{
				//		secondstartTime = new DateTime(2024, 8, 23, 0, 0, 0, 0);
				//		west_cutofftime = west_cutofftime.AddHours(-1);
				//	}

				//	if (us_dat > secondstartTime &&
				//		((second_eastWarehouseList.Any(p => p == match_WarehouseName) && us_dat > west_starttime && us_dat.AddHours(3) < west_cutofftime) || (second_westWarehouseList.Any(p => p == match_WarehouseName) && us_dat > west_starttime && us_dat < west_cutofftime)))
				//	{
				//		OrderMatch.SericeType = "FEDEX_GROUND";
				//		OrderMatch.Logicswayoid = 18;
				//		Helper.Info(Order.number, "wms_api", $"仓库：{OrderMatch.WarehouseName}，二次发货的时间阶段，去替换物流：(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType})");
				//	}
				//}
				#endregion



				#region 20022-04-18取消加州优先级 20022-04-20取消美国优先级
				//if (OrderMatch.WarehouseID == 82)
				//{
				//    //if (Order.temp10.ToUpper() == "CA")
				//    //{
				//    //    //EffectiveWarehouse为有库存的仓库列表
				//    //    var EffectiveWarehouse2 = EffectiveWarehouse.Where(o => o.number == 34 || o.number == 24 || o.number == 99).OrderBy(o => o.AreaSort).ToList();
				//    //    if (EffectiveWarehouse2.Any())
				//    //    {
				//    //        Helper.Info(Order.number, "wms_api", $"{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID} 去替换仓库(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType})");
				//    //        OrderMatch.Logicswayoid = BeforeModificationLogicswayoid;
				//    //        OrderMatch.SericeType = BeforeModificationSericeType;
				//    //        foreach (var item in EffectiveWarehouse2)
				//    //        {
				//    //            //var Warehouse = item.id;
				//    //            WarehouseLevel(item, configs, MatchRules, sheets,
				//    //                weight, weight_g, volNum, volNum2, weight_vol, weight_phy, maxNum, secNum, Tocountry, ref OrderMatch);

				//    //            if (item.oids.Contains("," + OrderMatch.Logicswayoid + ","))
				//    //            {
				//    //                IsFind2 = true;
				//    //                OrderMatch.WarehouseID = item.number;
				//    //                OrderMatch.WarehouseName = item.id;
				//    //                Helper.Info(Order.number, "wms_api", $"仓库替换为{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID}(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType }");
				//    //                break;
				//    //            }
				//    //        }
				//    //    }
				//    //}
				//    //else
				//    if (Order.temp10.ToUpper() == "US")
				//    {
				//        //EffectiveWarehouse为有库存的仓库列表
				//        var EffectiveWarehouse2 = EffectiveWarehouse.Where(o => o.number == 46 || o.number == 93).OrderBy(o => o.AreaSort).ToList();
				//        if (EffectiveWarehouse2.Any())
				//        {
				//            Helper.Info(Order.number, "wms_api", $"{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID} 去替换仓库(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType})");
				//            OrderMatch.Logicswayoid = BeforeModificationLogicswayoid;
				//            OrderMatch.SericeType = BeforeModificationSericeType;
				//            foreach (var item in EffectiveWarehouse2)
				//            {
				//                //var Warehouse = item.id;
				//                WarehouseLevel(item, configs, MatchRules, sheets,
				//                    weight, weight_g, volNum, volNum2, weight_vol, weight_phy, maxNum, secNum, Tocountry, ref OrderMatch);

				//                if (item.oids.Contains("," + OrderMatch.Logicswayoid + ","))
				//                {
				//                    IsFind2 = true;
				//                    OrderMatch.WarehouseID = item.number;
				//                    OrderMatch.WarehouseName = item.id;
				//                    Helper.Info(Order.number, "wms_api", $"仓库替换为{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID}(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType }");
				//                    break;
				//                }
				//            }
				//        }
				//    }
				//}

				#endregion



				#endregion

				#endregion

				#region upssurepost如果比fedex贵，就用fedex发
				if (sheetCpbh.Count() == 1 && OrderMatch.Logicswayoid == 24 && Tocountry == "US")
				{
					try
					{
						var good = sheetCpbh.FirstOrDefault();
						var ckid = OrderMatch.WarehouseID;
						var ware = db.scbck.Where(c => c.number == ckid).FirstOrDefault();
						var zips = _cacheZip.FirstOrDefault(z => z.Origin == ware.Origin && Order.zip.StartsWith(z.DestZip));
						var FedexPrice = 12.52M;
						var surepost = db.OSV_FreightCharge_US_All.Where(o => o.LogisticsID == 24 && o.Sku == good && o.zone == zips.GNDZone).FirstOrDefault();
						var UPSSurePostPrice = surepost != null ? surepost.Charge : 0;
						if (UPSSurePostPrice > FedexPrice)
						{
							OrderMatch.Logicswayoid = 18;
							OrderMatch.SericeType = "FEDEX_GROUND";
							Helper.Info(Order.number, "wms_api", $"SurePostPrice：{UPSSurePostPrice}，FedexPrice：{FedexPrice},将SurePost替换为Fedex");
						}
						else
						{
							Helper.Info(Order.number, "wms_api", $"SurePostPrice：{UPSSurePostPrice}，FedexPrice：{FedexPrice}");
						}
					}
					catch
					{

					}
				}
				#endregion

				#region 有效仓库唯一时物流优化
				if (!IsFind2)
				{
					var CurrentWarehouse = EffectiveWarehouse.First();
					var oid = OrderMatch.Logicswayoid;
					if (EffectiveWarehouse.Count == 1)
					{
						if (!CurrentWarehouse.oids.Contains("," + oid + ","))
						{
							if (OrderMatch.Logicswayoid == 4 && CurrentWarehouse.oids.Contains(",18,"))
							{
								OrderMatch.Logicswayoid = 18;
								OrderMatch.SericeType = "FEDEX_GROUND";
							}
							if ((OrderMatch.Logicswayoid == 3 || OrderMatch.Logicswayoid == 24) && CurrentWarehouse.oids.Contains(",4,"))
							{
								OrderMatch.Logicswayoid = 4;
								OrderMatch.SericeType = "UPSGround";
							}
						}
						OrderMatch.WarehouseID = CurrentWarehouse.number;
						OrderMatch.WarehouseName = CurrentWarehouse.id;
					}
				}
				#endregion

				#region 确认订单自物流是否能自送
				//                var IsNoFDS = false;
				//                if (Order.dianpu.ToUpper() == "FDSUS")
				//                {
				//                    var t = db.Database.SqlQuery<int>(string.Format(@"select count(1) from oms_fds_order_history where  dianpu='FDSUS'
				//and customer_group_id in (2,9,12,13,14,15,20,21,22,25,26) and orderid='{0}'", Order.OrderID)).ToList().First();
				//                    IsNoFDS = (t > 0);
				//                }
				//                ////取消自物流
				//                //IsNoFDS = true;
				//                var IsCancelledc = db.Database.SqlQuery<int>(string.Format(@"select count(1) from OS_ChangeLogistics_Msg where NID={0}", Order.number)).ToList().First();
				//                var IsCancelled = (IsCancelledc > 0);

				//                var IsChangeLogisticsc = db.Database.SqlQuery<int>(string.Format(@"select count(1) from wms_Order_Supplementary where RelID='{0}' and RelType='ChangeLogistics'", Order.number)).ToList().First();
				//                var IsChangeLogistics = (IsChangeLogisticsc > 0);

				//                var WarehouseID = OrderMatch.WarehouseName;
				//                var Warehouse = Warehouses.FirstOrDefault(f => f.id == WarehouseID);

				//                if (Order.temp10 == "US" && sheets.Count() == 1 && OrderMatch.WarehouseID != 0 && !IsTwoDays && !IsNoFDS && !IsCancelled && !IsChangeLogistics && !ISTest
				//                    && !Warehouse.realname.ToLower().StartsWith("restock"))
				//                {
				//                    //校验前先更新仓库
				//                    var db2 = new cxtradeModel();
				//                    var order = db2.scb_xsjl.Find(Order.number);
				//                    order.warehouseid = OrderMatch.WarehouseID;
				//                    db2.SaveChanges();

				//                    var list = db.tms_orders_condit.Where(f => f.item != "cpbh").ToList();

				//                    if (!list.Any(f => f.item == "dianpu".ToLower() && f.value.ToLower() == Order.dianpu.ToLower())
				//                        && !list.Any(f => f.item == "xspt".ToLower() && f.value.ToLower() == Order.xspt.ToLower())
				//                        && list.Any(f => f.item == "zip".ToLower() && Order.zip.StartsWith(f.value)))
				//                    {
				//                        var api = new HttpApi();
				//                        var result = api.CheckByPioneer(Order.number);
				//                        if (result.State == 1)
				//                        {
				//                            OrderMatch.Logicswayoid = 59;
				//                            OrderMatch.SericeType = "Pioneer_A";
				//                        }
				//                    }
				//                }
				#endregion

				#endregion
			}

			#endregion
		}

		/// <summary>
		/// 新版美国匹配 2024-06-20 V3
		/// </summary>
		/// <param name="Order">订单信息</param>
		/// <param name="OrderMatch">发货仓库和物流(返回值)</param>
		private void MatchLogisticsByUSV3(MatchModel model, List<string> _UPSSkuList, string datformat, bool isprime, ref OrderMatchResult OrderMatch)
		{
			#region 基础信息
			var db = new cxtradeModel();
			if (model.toCountry == "US")
			{
				model.statsa = model.statsa.Replace(" ", "").ToLower().Contains("california") ? "CA" : model.statsa;
				model.statsa = model.statsa.Replace(" ", "").ToLower().Contains("texas") ? "TX" : model.statsa;
				model.statsa = model.statsa.Replace(" ", "").ToLower().Contains("newjersey") ? "NJ" : model.statsa;
			}

			var zipsNO = _cacheZip.FirstOrDefault(z => model.zip.StartsWith(z.DestZip));

			if (zipsNO != null)
			{
				if ("HI,PR".Split(',').Contains(zipsNO.State))
				{
					OrderMatch.Result.Message = string.Format("匹配错误,州[{0}]不允许匹配!", model.statsa);
					return;
				}
			}
			//20250429 余芳芳 AK AS GU HI MP PR VI 这几个州帮忙设置一下 提示同上
			if ("AK,AS,GU,HI,MP,PR,VI".Split(',').Contains(model.statsa))
			{
				OrderMatch.Result.Message = string.Format("匹配错误,州[{0}]不允许匹配!", model.statsa);
				return;
			}

			var MatchRules = db.scb_WMS_MatchRule.Where(f => f.State == 1).ToList();//当前可以用的特殊匹配规则
			var Warehouses = db.Database.SqlQuery<ScbckModel>(@"select 
','+isnull((select cast(oid as nvarchar(50))+',' from scb_Logisticsmode where warehoues like '%'+scbck.id+'%' and IsEnable=1
FOR XML PATH('')),'') oids,number,realname, id, name, firstsend, AreaLocation, AreaSort, Origin, OriginSort,0 Sort,0 Levels, 
IsMatching, IsThird,0 EffectiveInventory  from scbck where countryid='01' and state=1 and  IsMatching=1 order by AreaSort").ToList();

			//三方订单仓库
			if (model.name == "wms")
				Warehouses = Warehouses.Where(f => f.IsThird == 1).ToList();
			else
				Warehouses = Warehouses.Where(f => f.IsThird == 0).ToList();

			var sheetCpbh = model.details.Select(f => f.cpbh).ToList();


			#region 20250811 夏媛媛 overstock北美店铺空气净化器的订单地址是加州，拦截取消
			var matchRules_air = MatchRules.Where(p => p.Type == "Ban_Overstock_CA_AirPurifier").FirstOrDefault();
			if (matchRules_air != null && model.xspt == "overstock" && model.statsa == "CA")
			{
				var matchRules_air_details = db.scb_WMS_MatchRule_Details.Where(p => p.RID == matchRules_air.ID && p.Type.ToLower() == model.dianpu.ToLower()).ToList();
				if (matchRules_air_details.Any() && matchRules_air_details.Any(p => model.details.Any(q => q.cpbh == p.Parameter)))
				{
					OrderMatch.Result.Message = $"店铺:{model.dianpu},加州禁售空气净化器（{string.Join(",", model.details.Select(p => p.cpbh))}），请取消!";
					return;
				}
			}

			#endregion



			#region 黑名单
			// 20231019 吴碧珊 21 KARENS WAY, BEAR, DE, 19701-5300这个地址麻烦先帮我设置下，拉黑不发的
			//20231120 史倩文 11 BELDEN PL 这个地址屏蔽一下吧
			//20231201 李积增 麻烦帮忙设置这个地址为黑名单。 10901 REED HARTMAN HWY STE 305, CINCINNATI, OH
			//20231220 吴碧珊 22 South Wind Drive 不发
			var blackAdressRule = MatchRules.FirstOrDefault(p => p.Type == "Blacklist" && p.Country == "US");
			var blackAdressList = db.scb_WMS_MatchRule_Details.Where(p => p.RID == blackAdressRule.ID && p.Type == "Address").ToList();//new List<string>() { "10833 WILSHIRE BLVD", "21 KARENS WAY", "11 BELDEN PL", "10901 REED HARTMAN HWY STE 305", "22 South Wind Drive", "3901 CONSHOHOCKEN AVE APT 8301" };
			var blackCustomIdList = db.scb_WMS_MatchRule_Details.Where(p => p.RID == blackAdressRule.ID && p.Type == "CustomId").ToList();
			if (blackAdressList.Any(p => p.Parameter.ToUpper() == model.adress.ToUpper()))
			{
				OrderMatch.Result.Message = $"此单疑似黑名单,请取消!";
				return;
			}
			if (model.country == "US" && model.zip == "06903-5118" && model.statsa == "CT" && model.city == "STAMFORD" && (model.adress.Contains("190 THORNRIDGE DR") || model.address1.Contains("190 THORNRIDGE DR")))
			{
				OrderMatch.Result.Message = $"此单疑似黑名单 请联系李积增确认下!";
				return;
			}
			if (model.country == "US" && model.zip == "90022-5205" && model.statsa == "CA" && model.city == "LOS ANGELES" && (model.adress.Contains("6055 GLOUCESTER ST") || model.address1.Contains("6055 GLOUCESTER ST")))
			{
				OrderMatch.Result.Message = $"此单疑似黑名单 请联系刘旭婕确认!";
				return;
			}
			if (model.email == "k6zm49srbxqdzql@marketplace.amazon.com")
			{
				OrderMatch.Result.Message = $"黑名单,该客人订单联系刘旭婕确认是否发货!";
				return;
			}
			if (model.adress.ToUpper() == "1142 W Orangethorpe ave".ToUpper() || model.adress.ToUpper() == "3172 NASA ST".ToUpper())
			{
				OrderMatch.Result.Message = $"该地址不允许发货，请取消并联系孙婷!";
				return;
			}

			//20251030 史倩文 7497 Boul.LaSalle, LASALLE, QC, H8P 1X2 这个地址加黑名单哦，目前只限制temu平台
			if (model.xspt == "temu" && model.adress.ToLower() == "7497 boul.lasalle")
			{
				OrderMatch.Result.Message = $"客人黑名单，请取消!";
				return;
			}

			//20241210 史倩文 fei ren   36726 OLIVE ST, NEWARK, CA, 94560 - 2950 设置拦截
			if (model.adress.ToUpper() == "36726 OLIVE ST")
			{
				OrderMatch.Result.Message = $"此单疑似黑名单，请取消并联系邵梦璠!";
				return;
			}

			if (model.fkzh != null && blackCustomIdList.Any(p => p.Parameter.ToUpper() == model.fkzh.ToUpper()))
			{
				OrderMatch.Result.Message = $"此客人 CustomId:{model.fkzh} 黑名单，请取消。";
				return;
			}

			#endregion
			#region 黑名单
			//美国本地罗马伞禁售
			var BlackList3 = new List<string>()
			{
				"OP70233","OP70234","OP70280","OP70376","NP10190","NP10191","NP10192","NP10287","NP10288"
			};
			if (model.toCountry.ToUpper() == "US")
			{
				foreach (var sheet in sheetCpbh)
				{
					foreach (var bl in BlackList3)
					{
						if (sheet.StartsWith(bl))
						{
							OrderMatch.Result.Message = $"{sheet}的美国禁售罗马伞货号，取消!";
							return;
						}
					}
				}
			}

			var BlackList = new List<string>()
			{
				"ES10183-A","EP24771-A","EP24049","EP24045","EP24771US","EP24042","ES10182US-WH","ES10183US-WH","ES10173US-WH","EP24770US","EP24382US","EP20412","FP10119US-GR",
				"EP22756US-BK","EP22756US-WH","EP24436US-BK","EP24436US-IV","EP24437US-IV","ES10043","ES10166US-WH","ES10167US-WH","FP10092US-WH",
				"FP10244US-BL","FP10244US-WH","FP10376US-WH","SP37409US-PI","SP37409US-BL","SP37409US-GN",
				"NP12212US-BK","NP12213US-BK","NP12214US-BK","NP12215US-CF","NP11782US-BK","NP11783US-BK","NP11784US-BK","NP11785US-CF"
			};
			if (model.toCountry.ToUpper() == "US" && model.statsa.ToUpper().StartsWith("CA"))
			{
				foreach (var sheet in sheetCpbh)
				{
					if (BlackList.Contains(sheet))
					{
						OrderMatch.Result.Message = $"{sheet}货号，加州政府不能卖，请取消!";
						return;
					}
				}

				//20240924 余芳芳  EP24458US  只有Newegg 店铺发加州时做拦截
				//20251104 史倩文 认证齐全了 加州可以正常销售
				//if (model.dianpu.ToLower() == "newegg" && model.details.Any(p => p.cpbh == "EP24458US"))
				//{
				//	OrderMatch.Result.Message = $"{model.dianpu}店铺 EP24458US的CA不打单,请通知客服取消!";
				//	return;
				//}
			}
			//EP24897US-BK 吴碧芸说这个货可以发货了
			//EP24897US-NY EP24897US-RE EP24897US-SL 梅微说可以发货了
			var BlackList2 = new List<string>()
			{
				"NP11020US","BE10019US-GR","BE10019US-PI","BB5748NY"
			};
			//2025-04-18 吴碧芸 BB5748PI BB5748NY BB5748GR BB5748BE 货号
			var BlackList4 = new List<string>()
			{
				"BB5748PI","BB5748NY","BB5748GR","BB5748BE","BB5693GR"
			};
			//var BlackList5 = new List<string>()
			//{
			//	"SP37610WH","SP37610RE","SP37610NY","SP37610BK","SP37610BK","SP37589SH-BK","SP37610BK-HPK","SP37068BK","SP37610RE","SP37068RE","SP37610RE-HPK","SP37610NY","SP37068NY","SP37610NY-HPK","SP37610WH","SP37068WH","SP37610WH-HPK","SP37589SH-WH"
			//};

			foreach (var sheet in sheetCpbh)
			{
				//梅微 EP24897US-RE  costway-giveaway  想当礼物赠品送给客人
				if (BlackList2.Contains(sheet))
				{
					if (model.dianpu.ToLower() == "costway-giveaway" && sheet == "EP24897US-RE")
					{

					}
					else
					{
						OrderMatch.Result.Message = $"{sheet}的全美不打单,请通知客服取消!";
						return;
					}
				}
				//2025-04-18 吴碧芸 BB5748PI BB5748NY BB5748GR BB5748BE 货号不能发 取消
				if (BlackList4.Contains(sheet))
				{
					OrderMatch.Result.Message = $"{sheet}的全美不打单,请通知客服取消!";
					return;
				}
				//if (BlackList5.Contains(sheet) && DateTime.Now < new DateTime(2025, 11, 29)) // 2025-11-17)
				//{
				//	OrderMatch.Result.Message = $"{sheet}的不打单,先hold，11.29再发!";
				//	return;
				//}
			}
			var BabyCarNeedCancel = MatchRules.FirstOrDefault(f => f.Type == "BabyCarNeedCancel");
			var BabyCarNeedCancelCpbhs = db.scb_WMS_MatchRule_Details.Where(f => f.RID == BabyCarNeedCancel.ID && f.Type == "ItemNo").Select(o => o.Parameter).ToList();
			var BabyCarNeedCancelCpbh = sheetCpbh.Where(t => BabyCarNeedCancelCpbhs.Contains(t)).FirstOrDefault();
			if (BabyCarNeedCancelCpbh != null && (model.toCountry.ToUpper() == "CA" || model.toCountry.ToUpper() == "US"))
			{
				OrderMatch.Result.Message = $"此单存在婴儿车禁售全美{BabyCarNeedCancelCpbh},请取消!";
				return;
			}

			var BabyCarNeedCancelOnlyCA = MatchRules.FirstOrDefault(f => f.Type == "BabyCarNeedCancelCA");
			var BabyCarNeedCancelOnlyCACpbhs = db.scb_WMS_MatchRule_Details.Where(f => f.RID == BabyCarNeedCancelOnlyCA.ID && f.Type == "ItemNo").Select(o => o.Parameter).ToList();
			var BabyCarNeedCancelOnlyCACpbh = sheetCpbh.Where(t => BabyCarNeedCancelOnlyCACpbhs.Contains(t)).FirstOrDefault();
			if (BabyCarNeedCancelOnlyCACpbh != null && model.toCountry.ToUpper() == "CA")
			{
				OrderMatch.Result.Message = $"此单存在婴儿车禁售加拿大{BabyCarNeedCancelOnlyCACpbh},请取消!";
				return;
			}

			if (model.toCountry.ToUpper() == "CA" && sheetCpbh.Any(t => !string.IsNullOrEmpty(t) && "BC10140GR,BC10256GR".Contains(t)))
			{
				OrderMatch.Result.Message = $"BC10140GR,BC10256GR 加拿大禁售！";
				return;
			}
			if (BlackAddress(model.dianpu, model.xspt, model.khname, model.adress, model.address1, model.city, model.toCountry, model.email, model.fkzh, model.phone, model.bz))
			{
				OrderMatch.Result.Message = $"退单率高，Hold单，请确认是否发货！";
				return;
			}
			#endregion

			#region 判断是否为卡车单

			var Transportation = MatchRules.FirstOrDefault(f => f.Type == "Transportation");
			var TransportationCpbhs = db.scb_WMS_MatchRule_Details.Where(f => f.RID == Transportation.ID && f.Type == "ItemNo").Select(o => o.Parameter).ToList();
			var TransportationCpbh = sheetCpbh.Where(t => TransportationCpbhs.Contains(t)).FirstOrDefault();
			if (TransportationCpbh != null)
			{
				OrderMatch.Result.Message = $"此单存在卡车单货物{TransportationCpbh},请手动处理!";
				return;
			}
			#endregion
			var country = model.country;
			var Tocountry = model.toCountry;
			var dianpuTrue = model.dianpu;
			if (string.IsNullOrEmpty(datformat))
				datformat = DateTime.Now.ToString("yyyy-MM-dd");

			#region CanCAToUS //////////////////////////////////

			var CanCAToUS = false;
			var CanSendCAWarehouseMatchRule = MatchRules.FirstOrDefault(f => f.Type == "CAToUSWarehouse");
			var CanSendCAWarehouseMatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == CanSendCAWarehouseMatchRule.ID).Select(o => o.Parameter).ToList();

			//调拨货号逻辑不要了
			var ban_FedexToUS = MatchRules.FirstOrDefault(p => p.Type == "NoCAToUSGood");
			var banFedexToUSGood_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == ban_FedexToUS.ID && f.Type == "ItemNo").Select(o => o.Parameter).ToList();

			var OnlyUPS = false;
			//只用ups
			if (MatchRules.Any(f => f.Type == "Only_UPSGround_Send"))
			{
				var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_UPSGround_Send");
				var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
				if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == model.dianpu.ToUpper()))
				{
					OnlyUPS = true;
				}
			}
			var AnyNoCanSendCAGood = sheetCpbh.Where(o => banFedexToUSGood_Details.Contains(o)).Any();

			#endregion

			#region 分体式空调			
			var isAircondition = false;
			var aircondition_box = string.Empty;
			var airconditionrule = MatchRules.FirstOrDefault(p => p.Type == "UnPack_AirconditionPart");
			if (model.details.Sum(p => p.sl) > 1 && airconditionrule != null)
			{
				var airconditionDetail = db.scb_WMS_MatchRule_Details.Where(p => p.RID == airconditionrule.ID).ToList();
				var airconditionParts = airconditionDetail.Where(p => p.Type == "part").Select(p => p.Parameter).ToList();
				if (model.details.Where(p => airconditionParts.Contains(p.cpbh)).Sum(p => p.sl) > 1)
				{
					isAircondition = true;
					aircondition_box = airconditionDetail.Where(p => p.Type == "box").Select(p => p.Parameter).FirstOrDefault();
				}
				if (isAircondition)
				{
					if (string.IsNullOrEmpty(aircondition_box))
					{
						OrderMatch.Result.Message = $"未获取到空白外箱货号，请联系运营确认！";
						return;
					}
					var notparts = model.details.Where(p => !airconditionParts.Contains(p.cpbh)).Select(p => p.cpbh);
					if (notparts.Any())
					{
						OrderMatch.Result.Message = $"存在非分布式空调的配件：{string.Join(",", notparts.ToList())} 请重新拆单";
						return;
					}
				}
			}
			#endregion

			#endregion

			#region 固定/指定物流

			#region 指定物流
			bool IsFind = true;
			bool IsAppoint = false;
			if (IsFind)
			{
				var order_carrier2 = db.Database.SqlQuery<scb_order_carrier>(string.Format("select * from scb_order_carrier where orderid='{0}' and dianpu='{1}' and oid is not null and oid!='0'"
					, model.orderId, model.dianpu)).ToList();
				if (order_carrier2.Any())
				{
					IsAppoint = true;
					var order_carrier = order_carrier2.First();
					OrderMatch.Logicswayoid = (int)order_carrier.oid;
					if (string.IsNullOrEmpty(order_carrier.ShipService))
					{
						if (OrderMatch.Logicswayoid == 4)
							order_carrier.ShipService = "UPSGround";
						else if (OrderMatch.Logicswayoid == 18)
							order_carrier.ShipService = "FEDEX_GROUND";

					}
					OrderMatch.SericeType = order_carrier.ShipService;
					//部分CA指定物流是UPS，我们这边就需要转成48，CA仓的UPS==UPSInternational
					if (country == "US" && (Tocountry == "CA" || Tocountry == "MX") && OrderMatch.Logicswayoid == 4)
					{
						OrderMatch.Logicswayoid = 48;
						OrderMatch.SericeType = "UPSInternational";
						//IsInternational = true;
					}
					if (model.dianpu.ToLower() == "fdsus" && (int)order_carrier.oid == 22)
					{
						OrderMatch.Result.Message = $"此单是{model.dianpu}卡车单,请手动处理!";
						return;
					}
					if ((int)order_carrier.oid == 34)
					{
						if (string.IsNullOrEmpty(order_carrier.temp))
						{
							OrderMatch.Result.Message = $"此单是{model.dianpu}自提单,请手动处理!";
							return;
						}
						else
						{
							int WarehouseID = int.Parse(order_carrier.temp);
							OrderMatch.WarehouseID = WarehouseID;
							OrderMatch.WarehouseName = db.scbck.FirstOrDefault(f => f.number == WarehouseID).id;
							return;
						}
					}

					//20231019 吴碧珊 针对wayfair平台补发重发不用指定仓库，无需提示，正常匹配就可以了
					if (model.xspt.ToLower() == "wayfair" && !string.IsNullOrEmpty(order_carrier.temp) && model.temp3 != "8" && model.temp3 != "4")
					{
						int WarehouseID = int.Parse(order_carrier.temp);
						var WarehouseName = db.scbck.FirstOrDefault(f => f.number == WarehouseID).id;
						foreach (var item in model.details)
						{
							var CurrentItemNo = item.cpbh;
							var sql = string.Format(@"select Warehouse WarehouseNo,kcsl-tempkcsl-unshipped-occupation EffectiveInventory,
kcsl ,tempkcsl,unshipped,occupation
from (
select Warehouse, SUM(sl) kcsl,
isnull((select SUM(sl) tempkcsl from scb_tempkcsl where nowdate = '{2}' and ck = Warehouse and cpbh = '{0}'), 0) tempkcsl,
isnull((select SUM(qty)  from tmp_pickinglist_occupy where cangku = Warehouse and cpbh = '{0}'), 0) unshipped,
isnull((select SUM(qty)  from ods_wmsus_Stock_PreOccupationSheet where WarehouseId = Warehouse and ItemNumber = '{0}' and Status=0 and IsValid =1), 0) occupation
from scb_kcjl_location a inner join scb_kcjl_area_cangw b
on a.Warehouse = b.Location and a.Location = b.cangw
where b.state = 0 and a.Disable = 0  and  a.LocationType<10 and b.cpbh = '{0}' and a.Warehouse='{3}'
group by Warehouse) t where kcsl - tempkcsl - unshipped - occupation >= {1}", CurrentItemNo, item.sl, datformat, WarehouseName);
							var list = db.Database.SqlQuery<WarehouseDetail>(sql).ToList();
							if (!list.Any())
							{
								string oldCpbh = CurrentItemNo;
								string newCpbh = RecommendCpbh(Tocountry, model.dianpu, oldCpbh, item.sl, country, model.xspt);
								if (oldCpbh != newCpbh)
								{
									OrderMatch.Result.Message += string.Format(" 推荐更换货号:{0}", newCpbh.TrimEnd(','));
									return;
								}
								else
								{
									OrderMatch.Result.Message += string.Format("wayfair平台 ，货号:{0}，{1} 指定仓库没货", oldCpbh, WarehouseName);
									return;
								}
							}
						}
					}

					if (OrderMatch.SericeType == "Transportation-wayfair")
					{
						//if ("wayfair-topbuy,wayfair-gymax,wayfairgw".Contains(model.dianpu.ToLower()))
						var wayfair = new List<string>
						{
							"wayfair-topbuy",
							"wayfair-gymax",
							"wayfairgw"
						};
						if (wayfair.Contains(model.dianpu.ToLower()))
						{
							string warehousename = string.Empty;
							try
							{
								int WarehouseID = int.Parse(order_carrier.temp);
								warehousename = db.scbck.FirstOrDefault(f => f.number == WarehouseID).id;
							}
							catch (Exception)
							{

							}
							OrderMatch.Result.Message = $"wayfair卡车单，指定仓{warehousename},请确认仓库后派单!";
						}
						else
						{
							//已指定物流和仓库 不需要继续匹配了
							int WarehouseID = int.Parse(order_carrier.temp);
							OrderMatch.WarehouseID = WarehouseID;
							OrderMatch.WarehouseName = db.scbck.FirstOrDefault(f => f.number == WarehouseID).id;
						}
						return;
					}
					//20231019 吴碧珊 针对wayfair平台补发重发不用指定仓库，无需提示，正常匹配就可以了
					//20250804 吴碧珊 针对wayfair平台补发重发使用指定仓库
					if (!string.IsNullOrEmpty(order_carrier.temp) && ((order_carrier.dianpu.ToLower().Contains("wayfair")) || order_carrier.dianpu.ToLower().Contains("skonyon")))// && model.temp3 != "4" && model.temp3 != "8"
					{
						//已指定物流和仓库 不需要继续匹配了
						int WarehouseID = int.Parse(order_carrier.temp);
						OrderMatch.WarehouseID = WarehouseID;
						OrderMatch.WarehouseName = db.scbck.FirstOrDefault(f => f.number == WarehouseID).id;
						return;
					}

					if (order_carrier.oid == 4 && order_carrier.dianpu.ToLower().Contains("overstock-supplier"))
					{
						OnlyUPS = true;
					}
				}

				#region Vendor ca
				if (model.xspt.Contains("Vendor") && Tocountry == "CA")
				{
					if (model.details.Sum(p => p.sl) > 1)
					{
						OrderMatch.Result.Message += string.Format("vendor CA订单不支持多包发货");
						return;
					}
					var CurrentItemNo = model.details.First();
					var sql = string.Format(@"select Warehouse WarehouseNo,kcsl-tempkcsl-unshipped-occupation EffectiveInventory,
					kcsl ,tempkcsl,unshipped,occupation
					from (
					select Warehouse, SUM(sl) kcsl,
					isnull((select SUM(sl) tempkcsl from scb_tempkcsl where nowdate = '{2}' and ck = Warehouse and cpbh = '{0}'), 0) tempkcsl,
					isnull((select SUM(qty)  from tmp_pickinglist_occupy where cangku = Warehouse and cpbh = '{0}'), 0) unshipped,
					isnull((select SUM(qty)  from ods_wmsus_Stock_PreOccupationSheet where WarehouseId = Warehouse and ItemNumber = '{0}' and Status=0 and IsValid =1), 0) occupation
					from scb_kcjl_location a inner join scb_kcjl_area_cangw b
					on a.Warehouse = b.Location and a.Location = b.cangw
					where b.state = 0 and a.Disable = 0  and  a.LocationType<10 and b.cpbh = '{0}' and a.Warehouse like 'US%'
					group by Warehouse) t where kcsl - tempkcsl - unshipped - occupation >= {1}", CurrentItemNo.cpbh, CurrentItemNo.sl, datformat);
					var list = db.Database.SqlQuery<WarehouseDetail>(sql).ToList();
					var vendor_ca_ckList = "US116,US21,US121,US15,US11".Split(',').ToList();
					list.RemoveAll(p => !vendor_ca_ckList.Any(o => o == p.WarehouseNo));

					//20251129 张亮 qq-vendor 只发-w店铺
					if (!model.dianpu.ToUpper().Contains("-W"))
					{
						list.RemoveAll(p => p.WarehouseNo == "US116");
						Helper.Info(model.number, "wms_api", $" qq-vendor 只发-w店铺,{JsonConvert.SerializeObject(list)}");
					}
					//优先从虚拟仓出
					if (list.Any(p => p.WarehouseNo == "US116" || p.WarehouseNo == "US21" || p.WarehouseNo == "US121"))
					{
						var Baselist = list.Where(p => p.WarehouseNo == "US116" || p.WarehouseNo == "US21" || p.WarehouseNo == "US121").OrderBy(p => p.WarehouseNo).First();
						OrderMatch.WarehouseID = db.scbck.FirstOrDefault(f => f.id == Baselist.WarehouseNo).number;
						OrderMatch.WarehouseName = Baselist.WarehouseNo;
						OrderMatch.Logicswayoid = 4;
						OrderMatch.SericeType = GetUSServceType(OrderMatch.Logicswayoid);
						return;
					}
					//可以发的中转仓
					else if (list.Any())
					{
						var Baselist = list.OrderByDescending(p => p.WarehouseNo).First();
						OrderMatch.WarehouseID = db.scbck.FirstOrDefault(f => f.id == Baselist.WarehouseNo).number;
						OrderMatch.WarehouseName = Baselist.WarehouseNo;
						OrderMatch.Logicswayoid = 48;
						OrderMatch.SericeType = GetUSServceType(OrderMatch.Logicswayoid);
						return;
					}
					//其他不管了
					else
					{
						OrderMatch.WarehouseID = 0;
						return;
					}
				}
				#endregion
			}
			#endregion

			#region 特殊指定
			if (!IsAppoint)
			{
				//只用fedex ItemNo
				if (MatchRules.Any(f => f.Type == "Only_FedexGround_Send"))
				{
					var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_FedexGround_Send");
					var MatchRule_Details2 = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "ItemNo").ToList();
					var ItemNo = model.details.FirstOrDefault().cpbh;
					if (MatchRule_Details2.Any(f => f.Parameter.ToUpper() == ItemNo.ToUpper()))
					{
						OrderMatch.Logicswayoid = 18;
						OrderMatch.SericeType = "FEDEX_GROUND";
					}
					//20241210 余芳芳 zimbala 这家店，ES10267BK 这个货号针对店铺设置只匹配fedex
					if (model.dianpu.ToLower() == "zimbala" && model.details.Any(p => p.cpbh == "ES10267BK"))
					{
						OrderMatch.Logicswayoid = 18;
						OrderMatch.SericeType = "FEDEX_GROUND";
					}
				}

				//只用ups
				if (MatchRules.Any(f => f.Type == "Only_UPSGround_Send"))
				{
					var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_UPSGround_Send");
					var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
					if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == model.dianpu.ToUpper()))
					{
						OrderMatch.Logicswayoid = 4;
						OrderMatch.SericeType = "UPSGround";
					}
				}
				//只用fedex
				if (MatchRules.Any(f => f.Type == "Only_FedexGround_Send"))
				{
					var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_FedexGround_Send");
					var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
					if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == model.dianpu.ToUpper()))
					{
						OrderMatch.Logicswayoid = 18;
						OrderMatch.SericeType = "FEDEX_GROUND";
					}
				}

				if (MatchRules.Any(f => f.Type == "Walmart_NextDay"))
				{
					var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Walmart_NextDay");
					if (MatchRule.Shop.Split(',').Contains(model.dianpu) && model.temp2 == "1")
					{
						OrderMatch.Logicswayoid = 4;
						OrderMatch.SericeType = "UPSGround";
					}
				}
			}

			if ("WalmartVendor,wayfair".ToUpper().Split(',').Contains(model.dianpu.ToUpper()))
			{
				foreach (var item in Warehouses)
				{
					item.oids = item.oids.Replace(",24,", ",");
				}
			}
			#endregion

			#region 仓库物流禁用ups和fedex互换
			if (string.IsNullOrEmpty(model.trackno))
			{
				if (MatchRules.Any(f => f.Type == "Ban_Fedex_ConvertUPSGround")
				&& OrderMatch.Logicswayoid == 18 && OrderMatch.SericeType == "FEDEX_GROUND")
				{
					var IsOk = false;
					var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Ban_Fedex_ConvertUPSGround");
					var MatchRule_DetailsByStore = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
					if (MatchRule_DetailsByStore.Any(f => f.Parameter.ToUpper() == model.dianpu.ToUpper()))
					{
						IsOk = true;
					}
					var MatchRule_DetailsByItem = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Item").ToList();
					if (MatchRule_DetailsByStore.Any(f => f.Parameter.ToUpper() == model.dianpu.ToUpper())) //错误
					{
						IsOk = true;
					}
					if (IsOk)
					{
						foreach (var item in Warehouses)
						{
							item.oids = item.oids.Replace(",18,", ",");
						}
					}
				}
			}

			#endregion

			#endregion

			#region 邮编校验+偏远邮编校验 这些地址无法匹配
			if (MatchRules.Any(f => f.Type == "Remote_Districts"))
			{
				var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Remote_Districts");
				var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
				if (!MatchRule_Details.Any(f => f.Parameter.ToUpper() == dianpuTrue.ToUpper()) && Tocountry == "US")
				{
					var statsa = string.Empty;
					var zips = _cacheZip.FirstOrDefault(z => model.zip.StartsWith(z.DestZip));
					if (zips != null)
					{
						if ("AK,HI,PR".Split(',').Contains(zips.State))
						{
							OrderMatch.Result.Message = string.Format("匹配错误,州[{0}]不允许匹配！ ", model.statsa);
							return;
						}
					}
					else
					{
						OrderMatch.Result.Message = string.Format("匹配错误,邮编[{0}]无效！ ", model.zip);
						return;
					}
				}

				var MatchRulePobox = MatchRules.FirstOrDefault(f => f.Type == "Remote_DistrictsByPobox");
				var MatchRulePobox_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
				if (!MatchRule_Details.Any(f => f.Parameter.ToUpper() == dianpuTrue.ToUpper()))
				{
					if (model.adress.ToLower().Replace(" ", "").Replace(".", "").Contains("pobox"))
					{
						OrderMatch.Result.Message = string.Format("匹配错误,PO Box不允许发货！ ", model.statsa);
						return;
					}
				}
			}
			#endregion

			#region 初始化重量尺寸等
			double weight = 0;
			double weightReal = 0;
			double weight_g = 0;
			double? volNum = 0; //周长
			double? volNum2 = 0; //体积
			var cpRealSizeinfos = new List<CpbhSizeInfo>();  //真实尺寸
			var cpExpressSizeinfos = new List<CpbhSizeInfo>();  //打单尺寸
			var sizes = new List<double?>();
			var sheetsgroup = model.details.Select(a => a.cpbh).Distinct().ToList();
			foreach (var sheet in sheetsgroup)
			{
				var qty = model.details.Where(a => a.cpbh == sheet).ToList().Sum(a => a.sl);
				#region yushu 货号 是预售产品
				var isExsitPrecbph = false;
				#region 老产品
				if (!isExsitPrecbph)
				{
					//吴碧珊 说按照实际库存走
					//var xspt = model.xspt;
					//if (xspt.ToUpper() == "AM")
					//{
					//    xspt = "amazon";
					//}
					//var isOldPrecbph = db.scb_Presale_Goods.Where(d => d.IsDelete == 0 && (d.country == "US" || d.country == "CA") && d.cpbh == sheet.cpbh && (d.dianpu == model.dianpu || string.IsNullOrEmpty(d.dianpu)) && (d.platform == xspt || string.IsNullOrEmpty(d.platform)) && d.starttime < DateTime.Now && (!d.endtime.HasValue || (d.endtime.Value > DateTime.Now))).Any();
					//if (isOldPrecbph)
					//{
					//    isExsitPrecbph = true;
					//}
				}
				#endregion
				#region 新产品
				if (!isExsitPrecbph)
				{
					var Precbph = db.scb_oms_new_prd.Where(d => d.cpbh == sheet && d.country == "US" && !d.is_stock).Any();
					if (Precbph)
					{
						isExsitPrecbph = true;
					}
				}
				#endregion
				if (isExsitPrecbph)
				{
					OrderMatch.Result.Message = string.Format("预售货号,请手动处理");
					return;
				}
				#endregion
				var good = db.N_goods.Where(g => g.cpbh == sheet && g.country == country && g.groupflag == 0).FirstOrDefault();
				var cpRealSizeinfo = new CpbhSizeInfo() { cpbh = sheet, qty = qty };
				cpRealSizeinfo.bzcd = (decimal)good.bzcd;
				cpRealSizeinfo.bzkd = (decimal)good.bzkd;
				cpRealSizeinfo.bzgd = (decimal)good.bzgd;
				cpRealSizeinfo.weight = (decimal)good.weight * qty / 1000 * 2.2046226m;
				cpExpressSizeinfos.Add(cpRealSizeinfo);

				var realsize = db.scb_realsize.Where(p => p.cpbh == sheet && p.country == country).FirstOrDefault();
				if (realsize != null)
				{
					cpRealSizeinfo.bzcd = (decimal)realsize.bzcd;
					cpRealSizeinfo.bzkd = (decimal)realsize.bzkd;
					cpRealSizeinfo.bzgd = (decimal)realsize.bzgd;
				}
				cpRealSizeinfos.Add(cpRealSizeinfo);

				sizes.Add(good.bzgd);
				sizes.Add(good.bzcd);
				sizes.Add(good.bzkd);
				weightReal = (double)good.weight * qty / 1000 * 2.2046226;
				weight += (double)good.weight_express * qty / 1000 * 2.2046226;
				weight_g += (double)good.weight_express * qty;
				volNum += (good.bzcd + 2 * (good.bzkd + good.bzgd)) * qty;
				volNum2 += good.bzcd * good.bzgd * good.bzkd * qty;

				var sizesum = sizes.Sum(f => f) * 2 - sizes.Max();
				if (sizesum > 165 && !isAircondition && !model.ignoreSize)
				{
					OrderMatch.Result.Message = string.Format("匹配错误,尺寸{0}>165 超过标准,请手动处理", sizesum);
					return;
				}
				if (weight > 150 && !model.ignoreSize)
				{
					OrderMatch.Result.Message = string.Format("匹配错误,实重{0}>150 超过标准,请手动处理", weight);
					return;
				}
				if (sizes.Max() > 108 && !model.ignoreSize)
				{
					OrderMatch.Result.Message = string.Format("匹配错误,长{0}>108 超过标准,请手动处理", sizes.Max());
					return;
				}
			}

			if (isAircondition)
			{
				var box = db.N_goods.Where(g => g.cpbh == aircondition_box && g.country == country && g.groupflag == 0).FirstOrDefault();
				if (box == null)
				{
					OrderMatch.Result.Message = string.Format("无分体式空调的空白纸箱 {0}的产品信息，请确认", aircondition_box);
					return;
				}
				cpRealSizeinfos = new List<CpbhSizeInfo>() { new CpbhSizeInfo() {
					bzcd = (decimal)box.bzcd,
					bzkd = (decimal)box.bzkd,
					bzgd = (decimal)box.bzgd,
					weight =(decimal)weightReal
				} };
				sizes = new List<double?>() { box.bzcd, box.bzkd, box.bzgd };
				volNum = (box.bzcd + 2 * (box.bzkd + box.bzgd)) * 1;
				volNum2 += box.bzcd * box.bzgd * box.bzkd * 1;
			}

			double weight_vol = (double)volNum2 / 250;
			double weight_phy = weight_vol > weight ? weight_vol : weight;
			double weight_vol_ca = (double)volNum2 / 225;
			double weight_phy_ca = weight_vol_ca > weight ? weight_vol_ca : weight;
			double? girth_in = 0;

			var maxNum = sizes.Max();
			var secNum = sizes.Where(f => f < maxNum).Max();
			if (sizes.Count(f => f < maxNum) <= sizes.Count - 2)
				secNum = maxNum;

			#endregion

			#region 指定仓库openbox
			if (!string.IsNullOrEmpty(model.chck))
			{
				if (model.chck.ToLower() == "openbox")
				{
					OrderMatch.WarehouseID = 30;
					OrderMatch.WarehouseName = "US95";
				}
			}
			#endregion

			#region 跑步机订单时间14号至16号只允许从最近两日内的仓库发货
			bool IsTwoDays = false;

			if (MatchRules.Any(f => f.Type == "TwoDays") && Tocountry == "US")
			{
				var TwoDays = MatchRules.FirstOrDefault(f => f.Type == "TwoDays");
				var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == TwoDays.ID).ToList();
				var startdate = new DateTime(2021, 09, 14);
				var enddate = startdate.AddDays(3);
				if (model.orderdate >= startdate && model.orderdate < enddate && MatchRule_Details.Any(f => model.details.Any(g => g.cpbh == f.Parameter)))
				{
					IsTwoDays = true;
				}
			}
			#endregion

			#region 匹配仓库和物流

			var ws = new Dictionary<string, List<ScbckModel>>();
			var states = string.Empty;

			#region 仓库清单

			var WarehouseIDList = new List<string>();
			var WarehouseList = new List<ScbckModel>();
			var cklist = Warehouses.Where(c => c.Origin != "").ToList();
			if (string.IsNullOrEmpty(OrderMatch.WarehouseName))
			{
				var sort_i = 0;
				if (!string.IsNullOrEmpty(model.zip))
				{
					var zips = _cacheZip.Where(z => model.zip.StartsWith(z.DestZip)).ToList();

					if (zips.Any())
					{
						if (IsTwoDays)
						{
							var mindays = zips.Min(g => g.TNTDAYS) + 1;
							zips = zips.Where(f => f.TNTDAYS <= mindays).ToList();

							if (!zips.Any())
							{
								OrderMatch.Result.Message = "跑步机系列无就近仓库有库存";
								return;
							}
						}

						zips = zips.OrderBy(z => z.GNDZone).ThenBy(z => z.TNTDAYS).ToList();
						if (string.IsNullOrEmpty(states))
							states = zips.First().State;
						foreach (var item in zips)
						{
							var Origin = item.Origin;
							var ct = cklist.Where(c => c.Origin == Origin).OrderBy(c => c.OriginSort);

							foreach (var item2 in ct)
							{
								sort_i += 1;
								item2.Sort = sort_i;
								item2.GNDZone = item.GNDZone;
								WarehouseList.Add(item2);
								WarehouseIDList.Add(item2.id);
							}
						}
						Helper.Info(model.number, "wms_api", $"根据scb_ups_zip表，符合要求的仓库有：{string.Join(",", WarehouseIDList)}");
					}
				}
			}
			#endregion

			#region 匹配仓库
			bool IsInternational = false;
			var lcks = new List<ScbckModel>();
			//国际件
			if (country == "US" && (Tocountry == "CA" || Tocountry == "MX") && !dianpuTrue.ToLower().Contains("grouponvendor-ca"))
			{
				//////////////
				OrderMatch.Logicswayoid = 48;
				OrderMatch.SericeType = "UPSInternational";
				IsInternational = true;

				var restockCK = db.scbck.Where(o => o.countryid == "01" && o.realname.Contains("restock") && o.id != "US121").Select(o => o.id).ToList();
				WarehouseList = cklist.Where(f => f.id != "US98").ToList();
				WarehouseList = WarehouseList.Where(f => !restockCK.Contains(f.id)).ToList();

				if (Tocountry == "CA")
				{
					var CA_Fedex = MatchRules.FirstOrDefault(f => f.Type == "CA_Fedex");
					var CA_Fedexs = new List<scb_WMS_MatchRule_Details>();
					if (CA_Fedex != null)
					{
						CA_Fedexs = db.scb_WMS_MatchRule_Details.Where(f => f.RID == CA_Fedex.ID && f.Type == "Warehouse").ToList();
						WarehouseList.Where(f => CA_Fedexs.Any(g => g.Parameter == f.id)).ToList().ForEach(x => { WarehouseList.Remove(x); });
					}
					if (Warehouses.Any(c => c.id == "US121"))
					{
						var tt = Warehouses.FirstOrDefault(c => c.id == "US121");
						tt.Sort = -99;
						lcks.Add(tt);
					}
					if (Warehouses.Any(c => c.id == "US21"))
					{
						var tt = Warehouses.FirstOrDefault(c => c.id == "US21");
						tt.Sort = -97;
						lcks.Add(tt);
					}

					if (CA_Fedexs.Any())
					{
						//CA的仓库排序靠AreaSort来的，在这边处理不会破坏他原有排序。
						var t = cklist.Where(c => CA_Fedexs.Any(g => g.Parameter == c.id)).ToList();
						int Sort_i = -95;
						foreach (var item in t)
						{
							Sort_i += 1;
							item.Sort = Sort_i;
							lcks.Add(item);
						}
					}
					//lcks = lcks.OrderBy(f => f.Sort).ToList();

					//CA部分邮编禁止发Fedex 相当于只能从US21发货
					var CANoZip = MatchRules.FirstOrDefault(f => f.Type == "CANoZip");
					if (CANoZip != null)
					{
						var CANoZips = db.scb_WMS_MatchRule_Details.Where(f => f.RID == CANoZip.ID && f.Type == "ZIP").ToList();
						if (CANoZips.Any(f => model.zip.StartsWith(f.Parameter)))
						{
							OrderMatch.Result.Message = string.Format("匹配错误,加拿大BC省特殊邮编[{0}]禁止发货！ ", model.zip);
							return;
						}
					}
					//对于目的地在YT、NT、NU的Intlocal订单，只能从US11发货，US17、US16、US06不能发。也就只能用Fedex发
					//2022-04-11US16和US06也用fedex打，所以去除代码判断
					//20240424 张洁楠 US11不发Fedex
					//20250113 张洁楠 对于目的地在YT、NT、NU的订单，如果是中转仓发货，只能从Tacoma仓库发出
					var CANoUPS = MatchRules.FirstOrDefault(f => f.Type == "CANoUPS");
					if (CANoUPS != null)
					{
						var CANoUPSs = db.scb_WMS_MatchRule_Details.Where(f => f.RID == CANoUPS.ID && f.Type == "ZIP").ToList();
						if (CANoUPSs.Any(f => model.zip.StartsWith(f.Parameter)))
						{
							//if (Warehouses.Any(c => c.id == "US17" || c.id == "US11" || c.id == "US08"))
							//{
							//	Warehouses = Warehouses.Where(f => f.id != "US17" && f.id != "US11" && f.id != "US08").ToList();
							//	lcks = lcks.Where(f => f.id != "US17" && f.id != "US11" && f.id != "US08").ToList();
							//}

							if (Warehouses.Any(c => c.id == "US15" || c.id == "US16" || c.id == "US11" || c.id == "US06"))
							{
								Warehouses = Warehouses.Where(f => f.id != "US15" && f.id != "US16" && f.id != "US11" && f.id != "US06").ToList();
								lcks = lcks.Where(f => f.id != "US15" && f.id != "US16" && f.id != "US11" && f.id != "US06").ToList();
							}
						}
					}

					//20250314 张洁楠 SP38245US这个货号如果是加拿大本地仓或者美国中转仓发的话，指定用FedEx或者FedExtoCA
					if (model.details.Any(p => p.cpbh == "SP38244US" || p.cpbh == "SP38245US"))
					{
						Warehouses = Warehouses.Where(f => f.id == "US21" || f.id == "US121" || f.id == "US17").ToList();
						lcks = lcks.Where(f => f.id == "US21" || f.id == "US121" || f.id == "US17").ToList();
					}
					lcks.AddRange(WarehouseList);
				}
				else if (Tocountry == "MX")
				{
					if (Warehouses.Any(c => c.id == "US121"))
					{
						Warehouses = Warehouses.Where(o => o.id != "US121").ToList();
						WarehouseList = WarehouseList.Where(o => o.id != "US121").ToList();
					}
					if (Warehouses.Any(c => c.id == "US21"))
					{
						Warehouses = Warehouses.Where(o => o.id != "US21").ToList();
						WarehouseList = WarehouseList.Where(o => o.id != "US21").ToList();
					}

					lcks.AddRange(WarehouseList);
				}
			}
			else if (WarehouseList.Any())
			{
				//west加入restock仓库匹配
				if (WarehouseList.First().AreaLocation == "west")
				{
					var us98 = Warehouses.FirstOrDefault(c => c.id == "US98");
					if (us98 != null)
					{
						us98.Sort = -50;
						lcks.Add(us98);
					}
				}

				lcks.AddRange(WarehouseList);
			}

			if (model.dianpu == "Costway-Jandy")
			{
				if (Warehouses.Any(f => f.id == "US07" || f.id == "US11"))
					Warehouses = Warehouses.Where(f => f.id == "US07" || f.id == "US11").ToList();
			}

			#endregion

			#region 分配
			var restockck = db.scbck.Where(o => o.countryid == "01" && o.realname.Contains("restock")).ToList();
			foreach (var item in model.details)
			{
				var CurrentItemNo = item.cpbh;

				if (ws.ContainsKey(CurrentItemNo))
				{
					continue;
				}

				var sql = string.Format(@"select Warehouse WarehouseNo,kcsl-tempkcsl-unshipped-occupation EffectiveInventory,
kcsl ,tempkcsl,unshipped,occupation
from (
select Warehouse, SUM(sl) kcsl,
isnull((select SUM(sl) tempkcsl from scb_tempkcsl where nowdate = '{2}' and ck = Warehouse and cpbh = '{0}'), 0) tempkcsl,
isnull((select SUM(qty)  from tmp_pickinglist_occupy where cangku = Warehouse and cpbh = '{0}'), 0) unshipped,
isnull((select SUM(qty)  from ods_wmsus_Stock_PreOccupationSheet where WarehouseId = Warehouse and ItemNumber = '{0}' and Status=0 and IsValid =1), 0) occupation
from scb_kcjl_location a inner join scb_kcjl_area_cangw b
on a.Warehouse = b.Location and a.Location = b.cangw
inner join scbck c on c.id=a.Warehouse and c.IsMatching=1 and c.state=1 and c.IsDelivery=1 and c.IsThird=0 and c.countryid='01'
where b.state = 0 and a.Disable = 0  and  a.LocationType<10 and b.cpbh = '{0}'
group by Warehouse) t where  Warehouse like 'US%' and kcsl - tempkcsl - unshipped-occupation >= {1}", CurrentItemNo, item.sl, datformat);
				var list = db.Database.SqlQuery<WarehouseDetail>(sql).ToList();
				list = list.Where(o => o.WarehouseNo != "US07").ToList();

				var fbascbcks = db.scbck.Where(o => o.countryid == "01" && o.realname.Contains("FBA")).ToList();
				var fbascbcksIDs = fbascbcks.Select(o => o.id).ToList();
				list = list.Where(o => !fbascbcksIDs.Contains(o.WarehouseNo)).ToList();

				Helper.Info(model.number, "wms_api", JsonConvert.SerializeObject(list));

				//200250425 武金凤 加拿大发美国的所有物流都停
				if (model.toCountry.ToUpper() == "US" && list.Any(o => o.WarehouseNo == "US21" || o.WarehouseNo != "US121"))
				{
					list = list.Where(p => p.WarehouseNo != "US21" && p.WarehouseNo != "US121").ToList();
				}

				if (model.toCountry.ToUpper() == "CA" && list.Any(o => o.WarehouseNo == "US04"))
				{
					list = list.Where(p => p.WarehouseNo != "US04").ToList();
				}


				#region Target 按照平台指定仓库发货  重发、补发正常匹配
				if ((model.dianpu.ToLower() == "target" || model.dianpu.ToLower() == "target-topbuy" || model.dianpu.ToLower() == "target-safstar" || model.dianpu.ToLower() == "kohls") && model.temp3 != "8" && model.temp3 != "4")
				{
					var carrier = db.scb_order_carrier.Where(p => p.OrderID == model.orderId && p.dianpu == model.dianpu).FirstOrDefault();
					if (carrier != null && !string.IsNullOrEmpty(carrier.temp))
					{
						var appoint_Wares = new List<string>();
						if (carrier.temp == "restock")
						{
							appoint_Wares = restockck.Where(p => p.state == 1 && p.IsMatching == 1 && p.countrys == model.country && p.IsThird == 0).Select(p => p.id).ToList();
						}
						else
						{
							//var appoint_Ware = int.Parse(carrier.temp);
							//var ck = Warehouses.First(p => p.number == appoint_Ware);
							//appoint_Wares = new List<string>() { ck.id };
							var appoint_Ware = carrier.temp.Split(',').Select(p => int.Parse(p)).ToList();
							appoint_Wares = Warehouses.Where(p => appoint_Ware.Contains(p.number)).Select(p => p.id).ToList();
						}

						if (list.Any(p => appoint_Wares.Contains(p.WarehouseNo)))
						{
							list = list.Where(p => appoint_Wares.Contains(p.WarehouseNo)).ToList();
							Helper.Info(model.number, "wms_api", $"{model.dianpu} 按照平台指定仓库发货：{string.Join(",", appoint_Wares)}");
						}
						else
						{
							OrderMatch.Result.Message += $" 匹配失败，{model.dianpu} 指定仓: {string.Join(",", appoint_Wares)} 无库存";
							OrderMatch.Result.Message += string.IsNullOrEmpty(carrier.ShipService) ? "" : $"，指定物流:{carrier.ShipService}";
							return;
						}
					}
				}
				#endregion

				#region 禁发restock
				//var restockck = db.scbck.Where(o => o.countryid == "01" && o.realname.Contains("restock")).ToList();
				var restockIDs = restockck.Select(o => o.id).ToList();
				bool isContainRestock = list.Any(o => restockIDs.Contains(o.WarehouseNo));
				//禁发restock FDSUS,FDSCA,costway-giveaway,tiktok-giantex,tiktok-deal,tiktok-costzon,
				//包含 tiktok 都不行
				var Ban_RestockRule = MatchRules.FirstOrDefault(f => f.Type == "Ban_Restock");
				var Ban_RestockRuleDetails = db.scb_WMS_MatchRule_Details.Where(f => f.RID == Ban_RestockRule.ID).ToList();
				var forbiddenRestock_dianpus = Ban_RestockRuleDetails.Where(p => p.Type == "Store").Select(p => p.Parameter).ToList();
				var forbiddenRestock_cpbhs = Ban_RestockRuleDetails.Where(p => p.Type == "cpbh").Select(p => p.Parameter).ToList();
				if (model.ignoreRestock)
				{
					list = list.Where(o => !restockIDs.Contains(o.WarehouseNo)).ToList();
					Helper.Info(model.number, "wms_api", "排除restock匹配");
				}
				//var forbiddenRestock_dianpus = new List<string>() { "fdsus", "fdsus-bp", "fdsca", "costway-giveaway", "petsjoy" };
				if (isContainRestock && (forbiddenRestock_dianpus.Contains(model.dianpu.ToLower()) || CurrentItemNo.ToUpper().StartsWith("JL") || forbiddenRestock_cpbhs.Contains(CurrentItemNo.ToUpper())))
				{
					list = list.Where(o => !restockIDs.Contains(o.WarehouseNo)).ToList();
					var message = $" {string.Join(",", forbiddenRestock_dianpus)},货号JL开头的订单,货号{string.Join(",", forbiddenRestock_cpbhs)},禁发restock";
					Helper.Info(model.number, "wms_api", message);
					if (list.Count == 0)
					{
						OrderMatch.Result.Message = message;
						return;
					}
				}
				//只有restock有货可发：重发，补发单+homedepot+fdsus-bp，进行提示，数据组手动派单即可
				//20240509 史倩文 fdsus-bp 禁发restock
				if (isContainRestock && (model.xspt.ToLower() == "homedepot" || model.temp3 == "8" || model.temp3 == "4"))
				{
					list = list.Where(o => !restockIDs.Contains(o.WarehouseNo)).ToList();
					if (list.Count == 0)
					{
						var message = "重发,补发单,homedepot,只有restock有货可发,需手动派单";
						Helper.Info(model.number, "wms_api", message);
						OrderMatch.Result.Message = message;
						return;
					}
				}
				//20251017 金兰  kohls不使用US15仓发
				//20251029 金兰 关闭
				//if (model.dianpu.ToLower() == "kohls" && list.Any(o => o.WarehouseNo == "US15"))
				//{
				//	list = list.Where(o => o.WarehouseNo != "US15").ToList();
				//	Helper.Info(model.number, "wms_api", "kohls店铺，不使用US15仓库");
				//}
				//20250915 余芳芳 这里的店铺名含tiktok的订单这个去掉吧 然后这个平台是优先发普通仓 普通仓没货了 再发restock仓 你那帮忙设置一下哈
				if (isContainRestock && model.xspt.ToLower().Contains("tiktok") && list.Any(o => !restockIDs.Contains(o.WarehouseNo)))
				{
					list = list.Where(o => !restockIDs.Contains(o.WarehouseNo)).ToList();
					Helper.Info(model.number, "wms_api", "店铺名含tiktok的订单当自发仓有货时不发restock，自发仓没货时restock可以发");
				}

				//20241029 武金凤 target当自发仓有货时不发restock，自发仓没货时restock可以发
				if (isContainRestock && model.xspt.ToLower() == "target" && list.Any(o => !restockIDs.Contains(o.WarehouseNo)))
				{
					list = list.Where(o => !restockIDs.Contains(o.WarehouseNo)).ToList();
					Helper.Info(model.number, "wms_api", "target当自发仓有货时不发restock，自发仓没货时restock可以发");
				}

				//20250903 吴碧珊 Walmart  CM22873  CM22728  优先发普通仓，再发restock
				//20250928 吴碧珊 walamrt店铺  CM22873  CM22728  CM20654  CM23468US	 优先发普通仓，再发restock
				if (isContainRestock && model.dianpu.ToLower() == "walmart" && (CurrentItemNo == "CM22873" || CurrentItemNo == "CM22728" || CurrentItemNo == "CM20654" || CurrentItemNo == "CM23468US") && list.Any(o => !restockIDs.Contains(o.WarehouseNo)))
				{
					list = list.Where(o => !restockIDs.Contains(o.WarehouseNo)).ToList();
					Helper.Info(model.number, "wms_api", $"自发仓有货时不发restock，自发仓没货时restock可以发");
				}

				//20250917 吴碧珊 产品JZ10137DK 店铺名untilgone的订单优先发普通仓，其次restock仓
				//20250918 吴碧珊 JZ10137NA  untilgone店铺,优先发普通仓，其次restock仓
				//20250919 吴碧珊 EP20819YW  untilgone店铺,优先发普通仓，其次restock仓
				if (isContainRestock && (CurrentItemNo == "JZ10137DK" || CurrentItemNo == "JZ10137NA" || CurrentItemNo == "EP20819YW") && model.xspt.ToLower().Contains("untilgone") && list.Any(o => !restockIDs.Contains(o.WarehouseNo)))
				{
					list = list.Where(o => !restockIDs.Contains(o.WarehouseNo)).ToList();
					Helper.Info(model.number, "wms_api", "产品JZ10137DK 店铺名untilgone的订单优先发普通仓，其次restock仓");
				}

				//20250327 史倩文 0金额的订单不发restock，如果只有restock，可发
				if (isContainRestock && model.xszje == 0 && model.details.Any(p => p.price == 0.001m) && list.Any(o => !restockIDs.Contains(o.WarehouseNo)))
				{
					list = list.Where(o => !restockIDs.Contains(o.WarehouseNo)).ToList();
					Helper.Info(model.number, "wms_api", "0金额的订单不发restock，如果只有restock，可发");
				}

				#endregion

				//20241129 史倩文 CM20653除verndor外，其他都发dallas
				if (item.cpbh == "CM20653" && model.xspt.ToLower() != "vendor" && list.Any(o => o.WarehouseNo == "US10"))
				{
					list = list.Where(p => p.WarehouseNo == "US10").ToList();
					Helper.Info(model.number, "wms_api", "CM20653 货号，除vendor平台外，优先发US10");
				}

				//20251128 吴碧珊 JV13311CF JV13311BN 都发Fontana
				//20251201 吴碧珊 不限制仓库了
				//if (model.details.Any(p => p.cpbh == "JV13311CF" || p.cpbh == "JV13311BN"))
				//{
				//	list = list.Where(p => p.WarehouseNo == "US06").ToList();
				//	Helper.Info(model.number, "wms_api", "JV13311CF JV13311BN 都发Fontana");
				//}


				//只用fedex
				//20240424 张洁楠 US11只用fedex
				//20250113 张洁楠 Tacoma仓库的加拿大订单统一发FedExtoCA
				if (MatchRules.Any(f => f.Type == "Only_FedexGround_Send") && Tocountry == "CA")
				{
					var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_FedexGround_Send");
					var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
					if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == model.dianpu.ToUpper()))
					{
						list = list.Where(o => o.WarehouseNo != "US15" && o.WarehouseNo != "US16" && o.WarehouseNo != "US11" && o.WarehouseNo != "US06").ToList();
					}
				}

				//20250617 史倩文  temu-costwayca，优先QQ，再Borden，只发这两个仓库，别的仓都不发
				//20250919 胡丕学 is_US_to_CA_BBC  temu-costwayca只发中转仓
				if (model.dianpu.ToLower() == "temu-costwayca" && model.toCountry == "CA" && !db.scb_order_carrier.Where(p => p.OrderID == model.orderId && p.temp2 == "is_US_to_CA_BBC").Any())
				{
					list = list.Where(p => p.WarehouseNo == "US21" || p.WarehouseNo == "US121" || p.WarehouseNo == "US11").ToList();
					Helper.Info(model.number, "wms_api", "temu-costwayca，优先QQ，再Borden，只发这两个仓库，别的仓都不发");
					if (list.Count == 0)
					{
						OrderMatch.Result.Message = "temu-costwayca，优先QQ，再Borden，只发这两个仓库，别的仓都不发";
						return;
					}
				}
				//temu-ca 只能发US11，US21
				//20240830 武金凤 temu加拿大可以发qq和所有中转仓  下周一开始
				if (model.xspt.ToLower() == "temu" && model.toCountry == "CA")
				{
					//如果有标记，则优先从中转仓发，中转仓优先级按我们系统来
					//20250828 胡丕学 加拿大的订单 QQ有货就先发QQ了 没货再来中转仓了，temu-costwayca 这个加本店铺的 不调整哦
					//20250915 胡丕学 is_US_to_CA_BBC 只发中转仓
					//20251023 金兰 优先本地 > 11>17>15>16>06 ,不区分BBC
					if (list.Any(p => p.WarehouseNo == "US11") && lcks.Any(p => p.id == "US11"))
					{
						list = list.Where(p => p.WarehouseNo == "US21" || p.WarehouseNo == "US121" || p.WarehouseNo == "US11").ToList();
					}
				}

				// 2025-09-10 史倩文 HCST03115=FP10791US-HS+ 发指定仓 Fontana Borden Houston
				// 20251201 梅微 这个规则去掉 
				//if (model.details.Any(p => p.sku == "HCST03115") && model.toCountry == "US")
				//{
				//	list = list.Where(p => p.WarehouseNo == "US06" || p.WarehouseNo == "US11" || p.WarehouseNo == "US12").ToList();
				//	Helper.Info(model.number, "wms_api", "HCST03115=FP10791US-HS+ 发指定仓 Fontana Borden Houston");
				//}
				//if (model.details.Any(p => p.sku == "HCST03289") && model.toCountry == "US")
				//{
				//	list = list.Where(p => p.WarehouseNo == "US06" || p.WarehouseNo == "US11" || p.WarehouseNo == "US12" || p.WarehouseNo == "US15").ToList();
				//	Helper.Info(model.number, "wms_api", "HCST03289=FP10792US-HS+ 发指定仓 Fontana Borden Houston Romeoville");
				//}
				if (model.dianpu.ToLower() == "shein-ca")
				{
					if (list.Any(a => a.WarehouseNo == "US21"))
					{
						list = list.Where(p => p.WarehouseNo == "US21").ToList();
						Helper.Info(model.number, "wms_api", "shein-ca只从US21发");
					}
					else
					{
						OrderMatch.Result.Message = "匹配失败！shein-ca只从US21发";
						return;
					}
				}

				#region vendor 注释，在上面指定发货规则单独处理了
				//if (model.xspt.ToLower() == "vendor" && model.toCountry == "CA")
				//{
				//	var carrier = db.scb_order_carrier.Where(p => p.OrderID == model.orderId).FirstOrDefault();

				//	if (list.Any(p => p.WarehouseNo == "US16" || p.WarehouseNo == "US06"))
				//	{
				//		list = list.Where(p => !(p.WarehouseNo == "US16" || p.WarehouseNo == "US06")).ToList();
				//		var msg = "vendor 加拿大 不发US06，US16,请确认！";
				//		Helper.Info(model.number, "wms_api", "vendor 加拿大订单不发US06，US16");
				//		if (list.Count == 0)
				//		{
				//			OrderMatch.Result.Message = "匹配失败！" + msg;
				//			return;
				//		}
				//	}

				//	if (carrier != null)
				//	{
				//		if (!string.IsNullOrEmpty(carrier.temp))
				//		{
				//			var warehouse = int.Parse(carrier.temp);
				//			var ck = Warehouses.First(p => p.number == warehouse);
				//			list = list.Where(p => p.WarehouseNo == ck.id).ToList();
				//			IsInternational = ck.Origin == "CA" ? false : IsInternational;
				//		}
				//		if ((carrier.oid ?? 0) > 0)
				//		{
				//			OrderMatch.Logicswayoid = (int)carrier.oid;
				//			OrderMatch.SericeType = GetUSServceType(OrderMatch.Logicswayoid);

				//			if (OrderMatch.SericeType.Contains("UPS"))
				//			{
				//				OnlyUPS = true;
				//			}
				//		}
				//	}
				//}
				#endregion

				#region CanCAToUS
				var isPriorityCACpbh = false;
				//美国跨国不发restock
				if (Tocountry == "US" && !OnlyUPS && CanSendCAWarehouseMatchRule_Details.Any(o => o == "US21") && list.Any(f => f.WarehouseNo == "US21") && !model.dianpu.ToLower().EndsWith("prime"))
				{
					var forbiddenCA_match = MatchRules.Where(p => p.Type == "Ban_FedexToUS").FirstOrDefault();
					var priorityCA = MatchRules.Where(p => p.Type == "CAToUS_Fedex").FirstOrDefault();
					var priorityCADetails = db.scb_WMS_MatchRule_Details.Where(p => p.RID == priorityCA.ID && p.Type == "ItemNo").Select(p => p.Parameter).ToList(); //ToUS，优先CA的货号
					var forbiddenCA_matchDetails = db.scb_WMS_MatchRule_Details.Where(p => p.RID == forbiddenCA_match.ID).ToList();
					var forbiddenCA_xspts = forbiddenCA_matchDetails.Where(p => p.Type == "platform").ToList(); //new List<string>() { "tiktok", "vendor", "temu", "shein", "target", "walmart" }; //, "tiktok-giantex", "tiktok-deal", "tiktok-costzon"
																												//var USNoStockToCA_xspmeits = new List<string>() { "temu", "shein", "target", "walmart" };
					var forbiddenCA_dianpus = forbiddenCA_matchDetails.Where(p => p.Type == "Store").ToList();

					//20240709 武金凤 请从今天开始设置temu，shein，target，walmart禁发FedExTOUS
					//var forbiddenCA_xspts = new List<string>() { "tiktok", "vendor", "temu", "shein", "target", "walmart" };
					//var USNoStockToCA_xspts = new List<string>() { "temu", "shein", "target", "walmart" };
					var matchcks = db.scbck.Where(p => p.countryid == "01" && p.IsMatching == 1 && p.state == 1 && (p.wh_type == "" || p.wh_type == "restock")).Select(p => p.id).ToList();
					list = list.Where(p => p.WarehouseNo.Contains("US") && matchcks.Contains(p.WarehouseNo)).ToList();
					var OLAKIDS_allowCpbhs = "TS10089PI-18,TS10240PI-16,TS10240RO-14,TS10240PI-14,TS10233DK-16,TS10233JDB-16,TS10233JDB-18,TS10233NY-16,TS10233NY-18,TS10240RO-18,TS10240ZS-18,TS10240PI-18,TS10233DK-18,TS10089PI-12,TS10178DK-16,TS10178DK-18,TS10178ZS-12,TS10178ZS-14,TS10178ZS-16,TS10178ZS-18,TS10178BL-12,TS10178BL-14,TS10178BL-16,TS10178BL-18,TS10178DK-12,TS10178DK-14,TS10185BL-16,TS10185BL-18,TS10185PI-16,TS10185PI-18,TS10089TU-12,TS10089TU-18,TS10089TU-14,TS10089TU-16".Split(',').ToList();
					var ULTIMATE_priorityCACpbhs = "SP37907BL,SP37907HS,SP37907RE,SP37905RE,SP37905HS,SP37905BL".Split(',').ToList();
					//20241017 邹璐 沃尔玛OLAKIDS店铺目这些SKU需要从加拿大发货到美国
					if (model.dianpu == "Olakids" && model.xspt == "Walmart" && !OLAKIDS_allowCpbhs.Any(p => sheetCpbh.Contains(p)))
					{
						list = list.Where(p => p.WarehouseNo != "US21" && p.WarehouseNo != "US121").ToList();
						var message = $"沃尔玛OLAKIDS店铺存在货号：{string.Join(",", sheetCpbh)}不支持CA发US";
						Helper.Info(model.number, "wms_api", message);
						if (list.Count == 0)
						{
							OrderMatch.Result.Message = "匹配失败！" + message;
							return;
						}
					}
					//20250331 余芳芳 SP37907BL,SP37907HS,SP37907RE,SP37905RE,SP37905HS,SP37905BL 这些货号 ULTIMATE店铺美国站点发美国本地仓库的货
					else if (model.dianpu == "ULTIMATE" && ULTIMATE_priorityCACpbhs.Any(p => sheetCpbh.Contains(p)) && list.Any(p => p.WarehouseNo != "US21" && p.WarehouseNo != "US121"))
					{
						list = list.Where(p => p.WarehouseNo != "US21" && p.WarehouseNo != "US121").ToList();
						var message = $"货号：{string.Join(",", sheetCpbh)}，ULTIMATE店铺美国站点发美国本地仓库的货";
						Helper.Info(model.number, "wms_api", message);
					}
					//指定平台不发CA的，排除CA仓
					else if (forbiddenCA_xspts.Any(p => p.Parameter.ToLower() == model.xspt.ToLower()) && model.dianpu != "Olakids")
					{
						list = list.Where(p => p.WarehouseNo != "US21" && p.WarehouseNo != "US121").ToList();
						var message = $"{model.xspt} 平台不支持CA发US";
						Helper.Info(model.number, "wms_api", message);
						if (list.Count == 0)
						{
							OrderMatch.Result.Message = "匹配失败！" + message;
							return;
						}
					}
					//指定店铺不发CA的，排除CA仓
					else if (forbiddenCA_dianpus.Any(p => p.Parameter.ToLower() == model.dianpu.ToLower()))
					{
						list = list.Where(p => p.WarehouseNo != "US21").ToList();
						var message = $"{model.dianpu} 店铺不支持CA发US";
						Helper.Info(model.number, "wms_api", message);
						if (list.Count == 0)
						{
							OrderMatch.Result.Message = "匹配失败！" + message;
							return;
						}
					}
					//20250217 武金凤 sku帮我设置 在发货时qq仓库的优先级最高，优先CA，CA没货发US
					else if (priorityCADetails.Any(p => sheetCpbh.Contains(p)) && list.Any(p => p.WarehouseNo == "US21"))
					{
						isPriorityCACpbh = true;
						CanCAToUS = true;
						list = list.Where(p => p.WarehouseNo == "US21").ToList();
						var message = $"货号：{string.Join(",", sheetCpbh)},优先CA，CA没货发US";
						Helper.Info(model.number, "wms_api", message);
					}

					//调拨货号，优先US，US没货才发CA
					else if (AnyNoCanSendCAGood && list.Any(p => p.WarehouseNo != "US21" && p.WarehouseNo != "US121"))
					{
						list = list.Where(p => p.WarehouseNo != "US21").ToList();
						var message = $"{model.details.First().cpbh} 属于调拨货号，优先出US，US没货才发CA";
						Helper.Info(model.number, "wms_api", message);
					}
					else
					{
						CanCAToUS = true;
					}
				}

				if (CanCAToUS)
				{
					if (list.Any(f => f.WarehouseNo == "US21") && CanSendCAWarehouseMatchRule_Details.Any(o => o == "US21"))
					{
						var exceedQtyLimit = false;

						if (list.Any(p => p.WarehouseNo != "US21" && p.WarehouseNo.Contains("US")))
						{
							var limitRule = MatchRules.FirstOrDefault(p => p.Type == "US21_FedexToUS");
							var limit = db.scb_WMS_MatchRule_Details.Where(p => p.RID == limitRule.ID && p.Type == "limit").First();
							if (!string.IsNullOrEmpty(limit.Parameter))
							{
								var limetQty = int.Parse(limit.Parameter);
								var dat = DateTime.Now;
								var count = db.Database.SqlQuery<int>(string.Format($@"select isnull(SUM(x.zsl),0) from scb_xsjl x 
				            where country = 'US' and x.temp10 = 'US'
				            and x.state = 0 and filterflag > 5 AND x.newdateis>'{dat.ToString("yyyy-MM-dd")}' AND x.newdateis<'{dat.AddDays(1).ToString("yyyy-MM-dd")}'
							AND x.hthm NOT LIKE 'Order%' AND x.hthm NOT LIKE 'return%'AND x.logicswayoid =79 and x.warehouseid =141")).FirstOrDefault();
								if (count > limetQty)
								{
									exceedQtyLimit = true;
									CanCAToUS = false;
								}
							}
						}
						var tt = Warehouses.FirstOrDefault(c => c.id == "US21");
						if (tt != null && !exceedQtyLimit)
						{
							CanCAToUS = true;
							tt.Sort = 10099;
							lcks.Add(tt);
						}

					}
					if (!CanSendCAWarehouseMatchRule_Details.Any(o => o == "US21"))
					{
						list = list.Where(o => o.WarehouseNo != "US21").ToList();
					}
				}
				//20241206 史倩文 CM22796superbuy店铺加拿大发
				if (model.dianpu.ToLower() == "superbuy" && sheetCpbh.Any(p => p == "CM22796") && list.Any(p => p.WarehouseNo == "US21"))
				{
					list = list.Where(p => p.WarehouseNo == "US21").ToList();
					Helper.Info(model.number, "wms_api", "superbuy店铺 CM22796 货号，优先加拿大发");
					CanCAToUS = true;
				}
				#endregion

				#region   优先本地仓
				var priorityLocalWareRule = MatchRules.FirstOrDefault(p => p.Type == "PriorityLocalWarehouse" && p.Country == "US");
				if (priorityLocalWareRule != null)
				{
					var isPriorityLocalWare = db.scb_WMS_MatchRule_Details.Any(p => p.RID == priorityLocalWareRule.ID && p.Parameter == item.cpbh);
					if (isPriorityLocalWare)
					{
						//ca已经优先本地仓了，us  us21的优先级比us11高，所以us有货，就不ca发了
						if (model.toCountry == "US" && list.Any(p => p.WarehouseNo != "US21" && p.WarehouseNo != "US121"))
						{
							CanCAToUS = false;
						}
						if (model.toCountry == "CA" && list.Any(p => p.WarehouseNo == "US21" || p.WarehouseNo == "US121"))
						{
							list = list.Where(p => (p.WarehouseNo == "US21" || p.WarehouseNo == "US121")).ToList();
						}
					}
				}
				#endregion

				//#region Oversize
				//var cpRealSizeinfo = cpRealSizeinfos.First();
				//var girth = cpRealSizeinfo.bzcd + 2 * (cpRealSizeinfo.bzkd + cpRealSizeinfo.bzgd);
				//var isOversize = Tocountry == "US" && model.details.Sum(p => p.sl) == 1 && (cpRealSizeinfo.bzcd > 96 || girth > 130) && cpRealSizeinfo.bzcd < 108 && girth < 165 && cpRealSizeinfo.weight < 150;
				//if (isOversize && list.Any(a => "US06,US16,US09".Contains(a.WarehouseNo)))
				//{
				//	OrderMatch.Result.Message = "Oversize 价格更新中,订单Hold";
				//	return;
				//}
				//#endregion

				#region 指定仓库  同款替换 指定仓库

				var cpbhReplaceInfo = db.scb_order_cpbh_Replace.Where(p => p.orderId == model.orderId && p.replaceType == WMSClassLibrary.Enums.EnumReplaceType.单品替换 && p.thhh == item.cpbh && !string.IsNullOrEmpty(p.appointWare)).OrderByDescending(p => p.createTime).FirstOrDefault();
				//isPriorityCACpbh 有些货号CA优先
				//重发、补发、不管了
				//20250421 退货排除同款替换 吴碧芸
				if (model.temp3 != "8" && model.temp3 != "4" && model.temp3 != "7" && cpbhReplaceInfo != null && !isPriorityCACpbh)
				{
					list = list.Where(p => cpbhReplaceInfo.appointWare == p.WarehouseNo).ToList();
					Helper.Info(model.number, "wms_api", $"同款替换，指定仓库：{JsonConvert.SerializeObject(cpbhReplaceInfo)}");
				}
				#endregion

				//只用ups
				if (MatchRules.Any(f => f.Type == "Only_UPSGround_Send") && model.toCountry == "US")
				{
					var MatchRule = MatchRules.FirstOrDefault(f => f.Type == "Only_UPSGround_Send");
					var MatchRule_Details = db.scb_WMS_MatchRule_Details.Where(f => f.RID == MatchRule.ID && f.Type == "Store").ToList();
					if (MatchRule_Details.Any(f => f.Parameter.ToUpper() == model.dianpu.ToUpper()) || OnlyUPS)
					{
						//list = list.Where(p => p.WarehouseNo != "US15" && p.WarehouseNo != "US04" && p.WarehouseNo != "US17").ToList();
						//Helper.Info(model.number, "wms_api", "指定发UPS店铺排除US15，US04，US17仓");
						list = list.Where(p => p.WarehouseNo != "US15").ToList();
						Helper.Info(model.number, "wms_api", "指定发UPS店铺排除US15仓");
					}
				}


				#region 20241213 武金凤 tacoma优先级最低，如果本地仓其他仓库有货，即使最便宜，也发其他仓
				if (model.toCountry == "US" && list.Any(p => p.WarehouseNo != "US17" && p.WarehouseNo != "US21" && p.WarehouseNo != "US121"))
				{
					list = list.Where(p => p.WarehouseNo != "US17").ToList();
					Helper.Info(model.number, "wms_api", "tacoma优先级最低");
				}
				#endregion

				#region 20240923 张洁楠  HV10064是从美国仓库发往加拿大仓库的订单，在US06或者US16有货，优先匹配这两个，指定物流为FedExtoCA，没货按照原本的发货规则进行匹配；
				//20250924 张洁楠 HV10064 优先发FedExtoCA，如果目前仓库发不了FedEx 那是可以匹配到ups to ca
				if (Tocountry == "CA" && item.cpbh == "HV10064" && list.Any(p => p.WarehouseNo == "US17"))
				{
					list = list.Where(p => p.WarehouseNo != "US15" && p.WarehouseNo != "US11" && p.WarehouseNo != "US06" && p.WarehouseNo != "US16").ToList();
				}
				#endregion


				if (OnlyUPS)
				{
					list = list.Where(p => p.WarehouseNo != "US10" && p.WarehouseNo != "US04" && p.WarehouseNo != "US17" && p.WarehouseNo != "US78" && p.WarehouseNo != "US136").ToList();
					if (list.Count == 0)
					{
						OrderMatch.Result.Message = string.Format("匹配错误,指定UPS，US10,US04,US17不能发UPS", OrderMatch.SericeType);
						return;
					}
				}


				#region CA 中转仓特殊规则
				//CA 本地仓有货，会优先本地仓
				if (model.toCountry == "CA" && !list.Any(p => p.WarehouseNo == "US21" || p.WarehouseNo == "US121"))
				{
					var dat = DateTime.Now;
					var startTime = new DateTime(dat.Year, dat.Month, dat.Day, 0, 0, 0);
					var endTime = startTime.AddDays(1);
					//20251106 张洁楠 US15的UPStoCA也是调到430单  tacoma的FedExtoCA每天限400单吧
					//20251113 张洁楠 US15的UPStoCA单量调整到550单吧
					//20251114 张杰楠 US15的UPStoCA单量调整到650单吧
					//20251124 张杰楠 US15的UPStoCA单量调整到1000单吧
					var rom_limt = 1000;
					//20251104张洁楠 fedextoca 50bl 350单 
					var tac_limt = 400;
					//周一要算两天的单量，因为周日不上班
					if (dat.DayOfWeek == DayOfWeek.Sunday)
					{
						startTime.AddDays(-1);
						tac_limt = tac_limt * 2;
						rom_limt = rom_limt * 2;
					}

					//US17单量限制300单
					if (list.Any(p => p.WarehouseNo == "US17"))
					{
						var tmpsql = $@"select isnull(SUM(x.zsl),0) from scb_xsjl(nolock) x  where country = 'US' and x.temp10 = 'CA'
and x.state = 0 and filterflag > 5 
AND x.newdateis>'{startTime.ToString("yyyy-MM -dd")}' AND x.newdateis<'{endTime.ToString("yyyy-MM -dd")}'
AND x.hthm NOT LIKE 'Order%' AND x.hthm NOT LIKE 'return%'AND x.logicswayoid in (78) and x.warehouseid in (116)";
						var qtytmp = db.Database.SqlQuery<int>(tmpsql).FirstOrDefault();

						//单量没超
						if (qtytmp <= tac_limt)
						{
							//40 以内优先发US17
							if (weight_phy_ca <= 50)
							{
								list = list.Where(p => p.WarehouseNo == "US17").ToList();
							}
							//超过40，优先其他中转
							else if (list.Any(p => p.WarehouseNo == "US11" || p.WarehouseNo == "US16" || p.WarehouseNo == "US06" || p.WarehouseNo == "US15"))
							{
								list = list.Where(p => p.WarehouseNo != "US17").ToList();
							}
						}
						else if (list.Any(p => p.WarehouseNo == "US11" || p.WarehouseNo == "US16" || p.WarehouseNo == "US06" || p.WarehouseNo == "US15"))
						{
							list = list.Where(p => p.WarehouseNo != "US17").ToList();
						}

					}

					//US15单量限制370单
					if (list.Any(p => p.WarehouseNo == "US15"))
					{
						var tmpsql = $@"select isnull(SUM(x.zsl),0) from scb_xsjl(nolock) x  where country = 'US' and x.temp10 = 'CA'
and x.state = 0 and filterflag > 5 
AND x.newdateis>'{startTime.ToString("yyyy-MM-dd")}' AND x.newdateis<'{endTime.ToString("yyyy-MM-dd")}'
AND x.hthm NOT LIKE 'Order%' AND x.hthm NOT LIKE 'return%'AND x.logicswayoid in (80) and x.warehouseid in (303)";
						var qtytmp = db.Database.SqlQuery<int>(tmpsql).FirstOrDefault();
						//单量限制也是中转仓中间限制，高于其他仓库
						if (qtytmp > rom_limt && list.Any(p => p.WarehouseNo == "US11" || p.WarehouseNo == "US16" || p.WarehouseNo == "US06" || p.WarehouseNo == "US17"))
						{
							list = list.Where(p => p.WarehouseNo != "US15").ToList();
						}
					}

				}

				#endregion


				ws.Add(item.cpbh, lcks.Where(f => list.Any(g => g.WarehouseNo == f.id)).ToList());
			}
			#endregion

			#endregion

			//初始化
			var EffectiveWarehouse = new List<ScbckModel>();
			var IsExistBySerice = !string.IsNullOrEmpty(OrderMatch.SericeType);//后续走尺寸重量标准判断物流
			var logisticsmodes = db.Database.SqlQuery<LogisticsWareRelationModel>("select oid logisticsId,operatoren logisticsName, warehoues  warehouseIds  from scb_Logisticsmode where IsEnable = 1 and warehoues like '%US%'").ToList(); //db.scb_Logisticsmode.Where(f => f.warehoues.Contains("US") && f.IsEnable == 1).ToList();

			#region 获取有效仓库
			if (ws.Any())
			{
				if (ws.Count() == 1)
				{
					if (ws.FirstOrDefault().Value.Any())
					{
						EffectiveWarehouse = ws.FirstOrDefault().Value;
					}
					else
					{
						#region 推荐替换货号   
						string oldCpbh = model.details.FirstOrDefault().cpbh;
						string newCpbh = RecommendCpbh(Tocountry, model.dianpu, oldCpbh, model.details.FirstOrDefault().sl, country, model.xspt);
						if (oldCpbh != newCpbh)
						{
							OrderMatch.Result.Message += string.Format(" 推荐更换货号:{0}", newCpbh.TrimEnd(','));
							return;
						}
						#endregion
					}
				}
				else
				{
					var Baselist = ws.FirstOrDefault().Value;
					foreach (var item in ws)
					{
						Baselist = Baselist.Where(f => item.Value.Any(g => g.id == f.id)).ToList();
					}
					if (Baselist.Any())
					{
						EffectiveWarehouse = Baselist;
					}
				}
			}

			if (!EffectiveWarehouse.Any())
			{
				OrderMatch.Result.Message = "匹配错误,库存不足";
				return;
			}
			else
			{
				//已指定物流
				if (IsExistBySerice)
				{
					//按照物流过滤有效仓库
					var oid2 = OrderMatch.Logicswayoid;
					var EffectiveWarehouse2 = EffectiveWarehouse.Where(f => f.oids.Contains("," + oid2 + ",")).ToList();
					Helper.Info(model.number, "wms_api", $"按照物流过滤有效仓库:{JsonConvert.SerializeObject(EffectiveWarehouse2)}");
					if (EffectiveWarehouse2.Any())
					{
						if (OnlyUPS)
						{
							EffectiveWarehouse2 = EffectiveWarehouse2.Where(p => p.id != "US10" && p.id != "US04" && p.id != "US17" && p.id != "US78" && p.id != "US136").ToList();
							if (!EffectiveWarehouse2.Any())
							{
								OrderMatch.Result.Message = string.Format("匹配错误,店铺只能发UPS，只有US10,US04,US17有货，US10,US04,US17不能发UPS", OrderMatch.SericeType);
								return;
							}

							if (model.toCountry == "CA")
							{
								EffectiveWarehouse2 = EffectiveWarehouse2.Where(p => p.id != "US17").ToList();
								if (!EffectiveWarehouse2.Any())
								{
									OrderMatch.Result.Message = string.Format("匹配错误,指定UPS，中转仓US17不能发UPS", OrderMatch.SericeType);
									return;
								}
							}

						}
						//(OrderMatch.SericeType == "UPSInternational" &&  Tocountry !="CA") MX不能发ups
						if (OrderMatch.SericeType == "UPSGround" || (OrderMatch.SericeType == "UPSInternational" && Tocountry != "CA"))
						{
							EffectiveWarehouse2 = EffectiveWarehouse2.Where(p => p.id != "US10" && p.id != "US17" && p.id != "US04" && p.id != "US78" && p.id != "US136").ToList();
							if (!EffectiveWarehouse2.Any())
							{
								OrderMatch.Result.Message = string.Format("匹配错误,订单指定只能发UPS，US10,US04,US17不能发UPS", OrderMatch.SericeType);

								return;
							}
						}

						//判断是否oversize ，且指定fedex，换fedex-3rd

						var cpRealSizeinfo = cpRealSizeinfos.First();
						var girth = cpRealSizeinfo.bzcd + 2 * (cpRealSizeinfo.bzkd + cpRealSizeinfo.bzgd);
						//20250212 武金凤 尺寸符合oversize，没走3rd的，匹配失败，请数据组手动派单
						//20250213 武金凤 平台或店铺有fedex的付款账号，也改成三方发
						if (Tocountry == "US" && model.details.Sum(p => p.sl) == 1
									&& (cpRealSizeinfo.bzcd > 96 || girth > 130) && cpRealSizeinfo.bzcd < 108 && girth < 165 && cpRealSizeinfo.weight < 150
									&& !_ThirdBillAccountList.Any(p => p.Logistics == "Fedex" && p.CountryCode == model.country && p.Shop.ToUpper() == model.dianpu.ToUpper())
									&& OrderMatch.SericeType.ToUpper().Contains("FEDEX")
								//&& fedex3rd_warehouses.Contains(warehouse.id)  //EffectiveWarehouse.Any(p => fedex3rd_warehouses.Contains(p.id)))
								)
						{
							var fedex3rds_ca = logisticsmodes.Where(p => p.logisticsName.StartsWith("FedEx-3rd_Ca")).FirstOrDefault();
							var fedex3rds_nj = logisticsmodes.Where(p => p.logisticsName.StartsWith("FedEx-3rd_Nj")).FirstOrDefault();

							if (EffectiveWarehouse2.Any(p => fedex3rds_ca.warehouseIds.Contains(p.id)))
							{
								EffectiveWarehouse2 = EffectiveWarehouse2.Where(p => fedex3rds_ca.warehouseIds.Contains(p.id)).ToList();
								OrderMatch.SericeType = fedex3rds_ca.logisticsName;
								OrderMatch.Logicswayoid = fedex3rds_ca.logisticsId;
							}
							else if (EffectiveWarehouse2.Any(p => fedex3rds_nj.warehouseIds.Contains(p.id)))
							{
								EffectiveWarehouse2 = EffectiveWarehouse2.Where(p => fedex3rds_nj.warehouseIds.Contains(p.id)).ToList();
								OrderMatch.SericeType = fedex3rds_nj.logisticsName;
								OrderMatch.Logicswayoid = fedex3rds_nj.logisticsId;
							}
							else
							{
								OrderMatch.Result.Message = string.Format("匹配错误,指定物流({0}),尺寸符合oversize，无符合Fedex-3rd仓库可以发货，请手动派单！", OrderMatch.SericeType);
								return;
							}


							////var fedex3rd_warehouses = string.Join(",", fedex3rds.Select(p => p.warehouseIds).ToList());
							////EffectiveWarehouse2 = EffectiveWarehouse2.Where(p => fedex3rd_warehouses.Contains(p.id)).ToList();
							//if (!EffectiveWarehouse2.Any())
							//{
							//	OrderMatch.Result.Message = string.Format("匹配错误,指定物流({0}),尺寸符合oversize，无符合Fedex-3rd仓库可以发货，请手动派单！", OrderMatch.SericeType);
							//	return;
							//}
							//OrderMatch.SericeType = "FedEx-3rd";
							//OrderMatch.Logicswayoid = 121;

						}

						var CurrentWarehouse2 = EffectiveWarehouse2.First();
						OrderMatch.WarehouseID = CurrentWarehouse2.number;
						OrderMatch.WarehouseName = CurrentWarehouse2.id;
						//国际件发货需要判断ups or fedex
						if (IsInternational)
						{
							if (CurrentWarehouse2.Origin == "CA")
								IsExistBySerice = false;
							else
							{
								OrderMatch.Logicswayoid = 48;
								OrderMatch.SericeType = "UPSInternational";
							}
						}
					}
					else
					{
						//仓库唯一时直接发指定物流
						if (EffectiveWarehouse.Count == 1)
						{
							IsExistBySerice = true;
							OrderMatch.WarehouseID = EffectiveWarehouse.First().number;
							OrderMatch.WarehouseName = EffectiveWarehouse.First().id;
						}
						else
						{
							OrderMatch.Result.Message = string.Format("匹配错误,指定物流({0})无符合仓库可以发货", OrderMatch.SericeType);
							return;
						}
					}
				}
			}
			#endregion

			#region 匹配合适的物流
			if ((IsExistBySerice && OrderMatch.WarehouseID != 0) || (model.xspt.ToLower() == "vendor" && model.toCountry == "CA" && OrderMatch.WarehouseID != 0 && OrderMatch.Logicswayoid != 0))
			{
				if (CanCAToUS && OrderMatch.Logicswayoid == 18 && (OrderMatch.WarehouseName == "US21" || OrderMatch.WarehouseName == "US121") && Tocountry == "US")
				{
					OrderMatch.Logicswayoid = 79;
					OrderMatch.SericeType = "FedexToUS";
				}
				return;//仓库和物流已匹配
			}
			else
			{
				bool IsFind2 = false;
				var configs = db.scb_WMS_LogisticsForWeight.Where(f => f.IsChecked == 1).ToList();

				#region 根据可发仓库查询
				//cpRealSizeinfos,
				IsFind2 = MatchActualWarehouseUSV3(restockck, _UPSSkuList, logisticsmodes, model, configs, MatchRules, cpRealSizeinfos, cpExpressSizeinfos, weightReal, weight, weight_g, volNum, volNum2, weight_vol, Tocountry, CanCAToUS, ref OrderMatch, ref EffectiveWarehouse, aircondition_box: aircondition_box);
				//IsFind2 = MatchActualWarehouseUSV3_tmp(restockck, _UPSSkuList, logisticsmodes, model, configs, MatchRules, cpRealSizeinfos, weightReal, weight, weight_g, volNum2, Tocountry, CanCAToUS, ref OrderMatch, ref EffectiveWarehouse, aircondition_box: aircondition_box);


				#region wjw的一些特殊要求

				#region oversize
				//var fedex3rd_warehouses = logisticsmodes.First(p => p.logisticsName == "FedEx-3rd").warehouseIds;
				//var cpRealSizeinfo = cpRealSizeinfos.First();
				//var girth = cpRealSizeinfo.bzcd + 2 * (cpRealSizeinfo.bzkd + cpRealSizeinfo.bzgd);
				//if (Tocountry == "US" && model.details.Sum(p => p.sl) == 1
				//	&& (cpRealSizeinfo.bzcd > 96 || girth > 130) && cpRealSizeinfo.bzcd < 108 && girth < 165 && cpRealSizeinfo.weight < 150
				//	&& (OrderMatch.SericeType.Contains("FEDEX") && !_ThirdBillAccountList.Any(p => p.Logistics == "Fedex" && p.CountryCode == model.country && p.Shop == model.dianpu))
				//	&& !string.IsNullOrEmpty(OrderMatch.WarehouseName) && fedex3rd_warehouses.Contains(OrderMatch.WarehouseName)  //EffectiveWarehouse.Any(p => fedex3rd_warehouses.Contains(p.id))
				//	)
				//{
				//	//US12 打单尺寸在130以下不发
				//	//20240626 张洁楠 US09 只能发FedExOVERSIZE
				//	if ((OrderMatch.WarehouseName == "US12" || OrderMatch.WarehouseName == "US09") && volNum <= 130)
				//	{

				//	}
				//	else if (fedex3rd_warehouses.Contains(OrderMatch.WarehouseName))
				//	{
				//		OrderMatch.SericeType = "FedEx-3rd";
				//		OrderMatch.Logicswayoid = 121;
				//	}
				//	//else
				//	//{
				//	//	OrderMatch.SericeType = "";
				//	//	OrderMatch.WarehouseID = 0;
				//	//	OrderMatch.WarehouseName = "";
				//	//}
				//}
				//20250328邵旭彬 只发fedex HW66067
				if (model.details.Any(p => p.cpbh == "HW66067") && model.dianpu == "BestbuyUS")
				{
					OrderMatch.SericeType = "FEDEX_GROUND";
					OrderMatch.Logicswayoid = 18;
					return;
				}
				//20250212 武金凤 尺寸符合oversize，没走3rd的，匹配失败，请数据组手动派单
				//20250213 武金凤 平台或店铺有fedex的付款账号，也改成三方发
				//20250414 武金凤 确认直接根据运费最优进行匹配，这个提示可以不用了，就注释了
				//if (Tocountry == "US" && model.details.Sum(p => p.sl) == 1
				//			&& (cpRealSizeinfo.bzcd > 96 || girth > 130) && cpRealSizeinfo.bzcd < 108 && girth < 165 && cpRealSizeinfo.weight < 150
				//			&& !_ThirdBillAccountList.Any(p => p.Logistics == "Fedex" && p.CountryCode == model.country && p.Shop == model.dianpu)  //OrderMatch.SericeType.Contains("FEDEX") &&
				//			&& fedex3rd_warehouses.Contains(warehouse.id)  //EffectiveWarehouse.Any(p => fedex3rd_warehouses.Contains(p.id)))
				//			)
				//{
				//	if (OrderMatch.Logicswayoid > 0 && !string.IsNullOrEmpty(OrderMatch.SericeType) && OrderMatch.Logicswayoid != 121)
				//	{
				//		OrderMatch.SericeType = "";
				//		OrderMatch.WarehouseID = 0;
				//		OrderMatch.WarehouseName = "";
				//		OrderMatch.Result.Message = "匹配失败！尺寸符合oversize，未发Fedex-3rd，请手动派单！";
				//		return;
				//	}
				//}

				#endregion

				//20250109 武金凤 fedex 优先发us10
				if (OrderMatch.Logicswayoid == 18 && model.toCountry == "US" && OrderMatch.WarehouseName != "US10" && EffectiveWarehouse.Any(p => p.id == "US10"))
				{
					var EffectiveWarehouse2 = EffectiveWarehouse.Where(o => o.id == "US10").FirstOrDefault();
					OrderMatch.WarehouseID = EffectiveWarehouse2.number;
					OrderMatch.WarehouseName = EffectiveWarehouse2.id;
					Helper.Info(model.number, "wms_api", $"fedex 优先发us10,仓库替换为{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID}(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType}");
				}

				//tangavendor这个店铺的订单匹配仓库的优先级进行调整，US11放在最后
				if (model.dianpu.ToLower().Equals("tangavendor"))
				{
					if (EffectiveWarehouse.Where(o => o.id == "US11").Any())
					{
						var last = EffectiveWarehouse.FirstOrDefault(f => f.id == "US11");
						if (last != null)
						{
							EffectiveWarehouse.Remove(last);
							EffectiveWarehouse.Add(last);
						}
					}
				}


				//20250710 武金凤 US15的匹配逻辑为 US08Chicago仓库优先于US15Romeoville新仓库
				if (OrderMatch.WarehouseID == 303 && OrderMatch.SericeType.ToLower().Contains("fedex") && EffectiveWarehouse.Any(p => p.id == "US08"))//&& CanReplaceWarehouse(EffectiveWarehouse2.id, logisticsmodes, OrderMatch.Logicswayoid, OrderMatch.SericeType)
				{
					//EffectiveWarehouse为有库存的仓库列表
					var EffectiveWarehouse2 = EffectiveWarehouse.Where(o => o.id == "US08").FirstOrDefault();
					OrderMatch.WarehouseID = EffectiveWarehouse2.number;
					OrderMatch.WarehouseName = EffectiveWarehouse2.id;
					Helper.Info(model.number, "wms_api", $"US08Chicago仓库优先于US15Romeoville新仓库,仓库替换为{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID}(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType}");
				}



				//2022-04-12wjw要求当匹配到US10，US12，US17，在US06，US16有货的前提下，替换当前仓库为US06，US16，其中US06优先级高于US16
				//判断仓库判断仓库后，在进行EffectiveWarehouse挑出US06和US16进行二次判断
				//2022-05-19 取消us12和us10  优先级
				//2022-11-16 调整了us06和us16优先级，uS16高于US06
				if (OrderMatch.WarehouseID == 116 && EffectiveWarehouse.Any(p => p.id == "US16" || p.id == "US06"))
				{
					//EffectiveWarehouse为有库存的仓库列表
					var EffectiveWarehouse2 = EffectiveWarehouse.Where(o => o.id == "US16" || o.id == "US06").OrderByDescending(o => o.OriginSort).FirstOrDefault();
					OrderMatch.WarehouseID = EffectiveWarehouse2.number;
					OrderMatch.WarehouseName = EffectiveWarehouse2.id;
					Helper.Info(model.number, "wms_api", $"仓库替换为{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID}(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType}");
				}

				//US12 Houston仓的发货优先级下调至US10Dallas
				//20231102 武金凤 US10 Dallas仓库计划于当地时间3号恢复一次拣货，US10 Dallas仓的发货优先级下调至US12 Houston仓库下面
				//20231107 武金凤 请将美国US10 Dallas仓库的优先级调为US10＞US12。
				if (OrderMatch.WarehouseID == 93 && !OrderMatch.SericeType.Contains("UPS") && EffectiveWarehouse.Any(p => p.id == "US10"))//&& CanReplaceWarehouse(EffectiveWarehouse2.id, logisticsmodes, OrderMatch.Logicswayoid, OrderMatch.SericeType)
				{
					//EffectiveWarehouse为有库存的仓库列表
					var EffectiveWarehouse2 = EffectiveWarehouse.Where(o => o.id == "US10").FirstOrDefault();
					OrderMatch.WarehouseID = EffectiveWarehouse2.number;
					OrderMatch.WarehouseName = EffectiveWarehouse2.id;
					Helper.Info(model.number, "wms_api", $"仓库替换为{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID}(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType}");
				}

				//优先级：US21 QQ＞US04 Dayton＞US11 Borden；
				if (CanCAToUS && EffectiveWarehouse.Any(p => p.id == "US21") && OrderMatch.SericeType.Contains("FEDEX"))
				{
					var needReplaceCks = "US04,US11";
					//周一 US04、US08、US11的fedex转换为US21，发FEDEXTOUS
					if (DateTime.Now.DayOfWeek == DayOfWeek.Monday)
					{
						needReplaceCks += ",US08";
					}
					if (needReplaceCks.Contains(OrderMatch.WarehouseName))
					{
						OrderMatch.WarehouseName = "US21";
						OrderMatch.WarehouseID = 141;
						OrderMatch.Logicswayoid = 79;
						OrderMatch.SericeType = GetUSServceType(OrderMatch.Logicswayoid);
						Helper.Info(model.number, "wms_api", $"仓库替换为{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID}(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType}");
					}
				}


				//20250528 武金凤  仓库优先级调整：周五 - 周日，当sav仓库的日单量达到1300单后，sav的优先级调整到bor后面。周一恢复
				var dat = DateTime.Now;
				if (OrderMatch.WarehouseID == 46 && OrderMatch.SericeType.Contains("FEDEX") && EffectiveWarehouse.Any(p => p.id == "US11") && (dat.DayOfWeek == DayOfWeek.Friday || dat.DayOfWeek == DayOfWeek.Saturday || dat.DayOfWeek == DayOfWeek.Sunday))
				{
					var qty1 = db.Database.SqlQuery<int>(string.Format($@"select isnull(SUM(x.zsl),0) from scb_xsjl(nolock) x 
								                where country = 'US' and x.temp10 = 'US'
								                and x.state = 0 and filterflag > 5 AND x.newdateis>'{dat.ToString("yyyy-MM-dd")}' AND x.newdateis<'{dat.AddDays(1).ToString("yyyy-MM-dd")}'
												AND x.hthm NOT LIKE 'Order%' AND x.hthm NOT LIKE 'return%'  and x.warehouseid in (46)")).ToList().FirstOrDefault();
					if (qty1 >= 1300)
					{
						//EffectiveWarehouse为有库存的仓库列表
						var EffectiveWarehouse2 = EffectiveWarehouse.Where(o => o.id == "US11").FirstOrDefault();
						OrderMatch.WarehouseID = EffectiveWarehouse2.number;
						OrderMatch.WarehouseName = EffectiveWarehouse2.id;
						Helper.Info(model.number, "wms_api", $"周五-周日，当sav仓库的日单量达到1300单后，US11>US09,仓库替换为{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID}(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType}");
					}
				}

				//US04Dayton的优先级调到高于US11Borden
				//US04，08，09的优先级调到高于US11Borden
				//US04>US08>US09
				//2023-03-07,去掉US09换us11，US04，08的优先级调到高于US11Borden
				//2023-03-09,去掉US09换us08，US04的优先级调到高于US11Borden
				//之前的需求
				//2023-04-03起北京时间周二到周五（包含周五）为US04＞US11，即当仓库为US11时用US04替换
				//2023-04-03起周六到周一（包含周一）为US04＞US08>US11，即当仓库为US11时用US04，US08替换，US04优先级比US08高
				//2023-04-15 为US04＞US08>US09>US11
				//2023-11-27 武金凤 都改成 US08 Chicago＞US04 Dayton>US11 Borden
				//2025-03-05 武金凤 sav fedex本地件发不了，需要转只转11 04 08
				//2025-05-28 武金凤 us08拿掉吧，影响不了多少，其他us04＞us11
				#region US11替换

				if (DateTime.Now.ToString("yyyy-MM-dd") == "2025-03-05" && OrderMatch.WarehouseID == 46 && OrderMatch.SericeType.Contains("FEDEX") && model.toCountry == "US")
				{
					if (EffectiveWarehouse.Any(p => p.id == "US11"))
					{
						var EffectiveWarehouse2 = EffectiveWarehouse.Where(p => p.id == "US11").FirstOrDefault();
						OrderMatch.WarehouseID = EffectiveWarehouse2.number;
						OrderMatch.WarehouseName = EffectiveWarehouse2.id;
						Helper.Info(model.number, "wms_api", $"仓库替换为{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID}(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType}");
					}
					else if (EffectiveWarehouse.Any(p => p.id == "US04"))
					{
						var EffectiveWarehouse2 = EffectiveWarehouse.Where(p => p.id == "US04").FirstOrDefault();
						OrderMatch.WarehouseID = EffectiveWarehouse2.number;
						OrderMatch.WarehouseName = EffectiveWarehouse2.id;
						Helper.Info(model.number, "wms_api", $"仓库替换为{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID}(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType}");
					}
					else if (EffectiveWarehouse.Any(p => p.id == "US08"))
					{
						var EffectiveWarehouse2 = EffectiveWarehouse.Where(p => p.id == "US08").FirstOrDefault();
						OrderMatch.WarehouseID = EffectiveWarehouse2.number;
						OrderMatch.WarehouseName = EffectiveWarehouse2.id;
						Helper.Info(model.number, "wms_api", $"仓库替换为{OrderMatch.WarehouseName}，id:{OrderMatch.WarehouseID}(物流ID：{OrderMatch.Logicswayoid}，SericeType：{OrderMatch.SericeType}");
					}
				}
				else if (OrderMatch.WarehouseID == 82 && OrderMatch.SericeType != "Xmile" && OrderMatch.SericeType != "Roadie" && OrderMatch.SericeType != "OnTrac" && OrderMatch.SericeType != "WESTERN_POST" && OrderMatch.SericeType != "Fimile" && !OrderMatch.SericeType.Contains("GOFO"))
				{
					var EffectiveWarehouse2 = EffectiveWarehouse.Where(p => p.id == "US04").OrderByDescending(o => o.number).FirstOrDefault();
					//if (DateTime.Now.DayOfWeek == DayOfWeek.Monday)
					//{
					//	EffectiveWarehouse2 = EffectiveWarehouse.Where(p => p.id == "US04" || p.id == "US08").OrderByDescending(o => o.number).FirstOrDefault();
					//}
					if (EffectiveWarehouse2 != null && CanReplaceWarehouse(EffectiveWarehouse2.id, logisticsmodes, OrderMatch.Logicswayoid, OrderMatch.SericeType))
					{
						OrderMatch.WarehouseID = EffectiveWarehouse2.number;
						OrderMatch.WarehouseName = EffectiveWarehouse2.id;
					}
				}
				#endregion

				#endregion

				#endregion
			}

			#endregion
		}

		private bool CanReplaceWarehouse(string replaceWarehouseId, List<LogisticsWareRelationModel> logisticsWareRelations, int logicswayoid, string sericeType)
		{
			var canreplace = true;
			var logisticsWareRelation = logisticsWareRelations.FirstOrDefault(p => p.logisticsId == logicswayoid);
			if (logisticsWareRelation != null)
			{
				if (logicswayoid == 121 || logicswayoid == 114)
				{
					var warehouseList = logisticsWareRelation.warehouseIds.Split(',').ToList();
					canreplace = warehouseList.Any(p => p == replaceWarehouseId);
				}
			}
			if (logicswayoid == 4)
			{
				canreplace = replaceWarehouseId != "US10";
			}
			return canreplace;

		}

		/// <summary>
		/// 推荐货号(与替换货号有点区别)
		/// 2023-03-30 调整一下，整个替换链都没货，用最后一个来替换
		/// </summary>
		/// <param name="temp10"></param>
		/// <param name="dianpu"></param>
		/// <param name="cpbh"></param>
		/// <param name="count"></param>
		/// <param name="country"></param>
		/// <param name="pingtai"></param>
		/// <returns></returns>
		public string RecommendCpbh(string temp10, string dianpu, string cpbh, int count, string country, string pingtai)
		{
			TempKcslHelper tempKcslHelper = new TempKcslHelper();
			OrderlogModel db = new OrderlogModel();
			string newCpbh = cpbh;
			string searchCountry = "";
			if (country.ToUpper() == "US")
			{
				searchCountry = temp10;
			}
			else
			{
				searchCountry = country;
			}
			//            string sql = $@"SELECT dAll.* FROM scb_GoodsReplace_Main m
			//INNER JOIN scb_SingleGoodsReplace_Detail d ON m.ID = d.MainID AND Type = 0
			//INNER JOIN scb_SingleGoodsReplace_Detail dAll ON d.MainID = dAll.MainID
			//WHERE d.Cpbh = '{cpbh}' AND m.country = '{searchCountry}' AND (m.Dianpu='{dianpu}' OR m.Dianpu='' OR m.Dianpu is null)  AND (m.Pingtai='{pingtai}' OR m.Pingtai=''  OR m.Pingtai is null)";

			string sql = $@"SELECT dAll.* FROM scb_GoodsReplace_Main m
INNER JOIN scb_SingleGoodsReplace_Detail d ON m.ID = d.MainID AND Type = 0
INNER JOIN scb_SingleGoodsReplace_Detail dAll ON d.MainID = dAll.MainID
WHERE d.Cpbh = '{cpbh}' AND dAll.country = '{searchCountry}' AND (dAll.Dianpu='{dianpu}' OR dAll.Dianpu='' OR dAll.Dianpu is null)  AND (dAll.Pingtai='{pingtai}' OR dAll.Pingtai=''  OR dAll.Pingtai is null)";
			List<scb_SingleGoodsReplace_Detail> scb_SingleGoodsReplace_Detail_List = db.Database.SqlQuery<scb_SingleGoodsReplace_Detail>(sql).ToList();
			if (scb_SingleGoodsReplace_Detail_List.Any())
			{
				List<scb_SingleGoodsReplace_Detail> scb_SingleGoodsReplace_Detail_OtherList = scb_SingleGoodsReplace_Detail_List.OrderBy(p => p.SortID).ToList();
				foreach (var item in scb_SingleGoodsReplace_Detail_OtherList)
				{
					if (tempKcslHelper.IsGoodsEnough(item.Cpbh, temp10, DateTime.Now.ToString("yyyy-MM-dd"), count - 1, country).Item1)
					{
						newCpbh = item.Cpbh;
						break;
					}
					//else
					//{
					//    newCpbh = item.Cpbh;
					//}
				}
			}
			return newCpbh;
		}

		private void WarehouseLevel(ScbckModel item, List<string> _UPSSkuList,
			List<scb_WMS_LogisticsForWeight> configs, List<scb_WMS_MatchRule> MatchRules, scb_xsjl Order, List<scb_xsjlsheet> sheets,
			double weight, double weight_g, double? volNum, double? volNum2, double weight_vol, double weight_phy, double? maxNum, double? secNum, string Tocountry,
		   bool CanCAToUS, ref OrderMatchResult OrderMatch, bool isAMZN, string aircondition_box = "")
		{
			var Warehouse = item.id;
			var config = configs.FirstOrDefault(f => f.Warehouses.Contains(Warehouse));
			if (config == null)
				config = configs.FirstOrDefault(f => f.Sort == 1);
			var weight_c = weight;
			if (config.Weight_Type == 1)
				weight_c = weight_vol;
			else if (config.Weight_Type == 2)
				weight_c = weight_phy;

			var cpbhs = string.IsNullOrEmpty(aircondition_box) ? sheets.Select(p => p.cpbh).ToList() : new List<string>() { aircondition_box };

			var ItemNo = cpbhs.First();
			if (CanCAToUS && (Warehouse == "US21" || Warehouse == "US121") && Tocountry == "US")
			{
				OrderMatch.Logicswayoid = 79;
				OrderMatch.SericeType = "FedexToUS";
				return;
			}

			var smartposts = MatchRules.Where(f => f.Type == "Allow_SmartPost" && f.State == 1).ToList();
			var ups_weight = volNum2 / 300;
			//加拿大本地件,计费重在40lbs以下的都给UPS，其余给FedEx
			if ((Warehouse == "US21" || Warehouse == "US121") && Tocountry == "CA")
			{
				ups_weight = volNum2 / 320;
				//计费重
				double jfweight = (double)(ups_weight > weight ? ups_weight : weight);
				if (jfweight > double.Parse(config.Weight))
				{
					OrderMatch.Logicswayoid = 18;
					OrderMatch.SericeType = "FEDEX_GROUND";
				}
				else
				{
					OrderMatch.Logicswayoid = 48;
					OrderMatch.SericeType = "UPSInternational";
				}
			}
			else
			{
				//计费重
				double jfweight = (double)(ups_weight > weight ? ups_weight : weight);
				if (smartposts.Any(f => f.ItemNo.Contains(ItemNo + ",")))
				{
					OrderMatch.Logicswayoid = 18;
					OrderMatch.SericeType = "FEDEX_GROUND";
				}
				else if (weight < 1)
				{
					if (weight_g <= 453)
					{
						OrderMatch.Logicswayoid = 3;
						OrderMatch.SericeType = "First|Default|Parcel|OFF";
					}
					else
					{
						OrderMatch.Logicswayoid = 24;
						OrderMatch.SericeType = "UPSSurePost";
					}
				}
				else if (1 <= jfweight && jfweight < 9 && maxNum < 30 && secNum < 30)
				{
					OrderMatch.Logicswayoid = 24;
					OrderMatch.SericeType = "UPSSurePost";
				}

				if (OrderMatch.SericeType != "First|Default|Parcel|OFF")
				{
					if (IsAMZNXsptOrDianpu(MatchRules, Order.xspt, Order.dianpu, Order.email))
					{
						if (isAMZN)
						{
							OrderMatch.Logicswayoid = 114;
							OrderMatch.SericeType = "AMZN";
						}
						else
						{
							OrderMatch.Logicswayoid = 18;
							OrderMatch.SericeType = "FEDEX_GROUND";
						}
					}
					else if (!_UPSSkuList.Any(p => cpbhs.Contains(p)))
					{
						OrderMatch.Logicswayoid = 18;
						OrderMatch.SericeType = "FEDEX_GROUND";
					}
				}

				//保底upsground
				if (string.IsNullOrEmpty(OrderMatch.SericeType))
				{
					OrderMatch.Logicswayoid = 4;
					OrderMatch.SericeType = "UPSGround";
					//OrderMatch.Logicswayoid = 18;
					//OrderMatch.SericeType = "FEDEX_GROUND";                  
				}

				if (Tocountry != "US" && OrderMatch.Logicswayoid != 18)
				{
					OrderMatch.Logicswayoid = 48;
					OrderMatch.SericeType = "UPSInternational";
				}
			}
		}

		private void WarehouseLevelV3(ScbckModel item, List<string> _UPSSkuList,
		List<scb_WMS_LogisticsForWeight> configs, List<scb_WMS_MatchRule> MatchRules, MatchModel model,
		double weight, double weight_g, double? volNum, double? volNum2, double weight_vol, double weight_phy, double? maxNum, double? secNum, string Tocountry,
		bool CanCAToUS, ref OrderMatchResult OrderMatch, bool isAMZN)
		{
			var Warehouse = item.id;
			var config = configs.FirstOrDefault(f => f.Warehouses.Contains(Warehouse));
			if (config == null)
				config = configs.FirstOrDefault(f => f.Sort == 1);
			var weight_c = weight;
			if (config.Weight_Type == 1)
				weight_c = weight_vol;
			else if (config.Weight_Type == 2)
				weight_c = weight_phy;

			var ItemNo = model.details.First().cpbh;
			if (CanCAToUS && (Warehouse == "US21" || Warehouse == "US121") && Tocountry == "US")
			{
				OrderMatch.Logicswayoid = 79;
				OrderMatch.SericeType = "FedexToUS";
				return;
			}

			var smartposts = MatchRules.Where(f => f.Type == "Allow_SmartPost" && f.State == 1).ToList();
			var ups_weight = volNum2 / 300;
			//加拿大本地件,计费重在40lbs以下的都给UPS，其余给FedEx
			if ((Warehouse == "US21" || Warehouse == "US121") && Tocountry == "CA")
			{
				ups_weight = volNum2 / 320;
				//计费重
				double jfweight = (double)(ups_weight > weight ? ups_weight : weight);
				if (jfweight > double.Parse(config.Weight))
				{
					OrderMatch.Logicswayoid = 18;
					OrderMatch.SericeType = "FEDEX_GROUND";
				}
				else
				{
					OrderMatch.Logicswayoid = 48;
					OrderMatch.SericeType = "UPSInternational";
				}
			}
			else
			{
				//计费重
				double jfweight = (double)(ups_weight > weight ? ups_weight : weight);
				if (smartposts.Any(f => f.ItemNo.Contains(ItemNo + ",")))
				{
					OrderMatch.Logicswayoid = 18;
					OrderMatch.SericeType = "FEDEX_GROUND";
				}
				else if (weight < 1)
				{
					if (weight_g <= 453)
					{
						OrderMatch.Logicswayoid = 3;
						OrderMatch.SericeType = "First|Default|Parcel|OFF";
					}
					else
					{
						OrderMatch.Logicswayoid = 24;
						OrderMatch.SericeType = "UPSSurePost";
					}
				}
				else if (1 <= jfweight && jfweight < 9 && maxNum < 30 && secNum < 30)
				{
					OrderMatch.Logicswayoid = 24;
					OrderMatch.SericeType = "UPSSurePost";
				}
				var cpbhs = model.details.Select(p => p.cpbh).ToList();
				if (OrderMatch.SericeType != "First|Default|Parcel|OFF")
				{
					if (IsAMZNXsptOrDianpu(MatchRules, model.xspt, model.dianpu, model.email))
					{
						if (isAMZN)
						{
							OrderMatch.Logicswayoid = 114;
							OrderMatch.SericeType = "AMZN";
						}
						else
						{
							OrderMatch.Logicswayoid = 18;
							OrderMatch.SericeType = "FEDEX_GROUND";
						}
					}
					else if (!_UPSSkuList.Any(p => cpbhs.Contains(p)))
					{
						OrderMatch.Logicswayoid = 18;
						OrderMatch.SericeType = "FEDEX_GROUND";
					}
				}

				//保底upsground
				if (string.IsNullOrEmpty(OrderMatch.SericeType))
				{
					OrderMatch.Logicswayoid = 4;
					OrderMatch.SericeType = "UPSGround";
					//OrderMatch.Logicswayoid = 18;
					//OrderMatch.SericeType = "FEDEX_GROUND";                  
				}

				if (Tocountry != "US" && OrderMatch.Logicswayoid != 18)
				{
					OrderMatch.Logicswayoid = 48;
					OrderMatch.SericeType = "UPSInternational";
				}
			}
		}

		private bool CanSendByZPL(decimal number, string cpbh, string warehouseID, int Logicswayoid)
		{
			try
			{
				//if (Logicswayoid == 24)
				//{
				//    Helper.Info(number, "wms_api", $"SUREPOST不支持ZPL,不支持传送带");
				//    return false;
				//}

				if (Logicswayoid == 59 || Logicswayoid == 114)
				{
					Helper.Info(number, "wms_api", $"不支持ZPL,不支持传送带");
					return false;
				}
				#region 仓库启用传送带功能(主仓和自仓需要分别判断) 
				if (warehouseID != "US16")
				{
					//Helper.Info(number, "wms_api", $"仓库{warehouseID}不支持传送带");
					return false;
				}
				#endregion


				var db = new cxtradeModel();
				scb_xsjl Order = db.scb_xsjl.Find(number);

				if (Order != null)
				{
					if (Logicswayoid == 59)
					{
						//Helper.Info(number, "wms_api", $"自物流不支持ZPL,不支持传送带");
						return false;
					}
					if (Order.temp10 != "US")
					{
						//Helper.Info(number, "wms_api", $"国际件不支持ZPL,不支持传送带");
						return false;
					}
					if (Order.dianpu.ToUpper() == "TARGET-TOPBUY" || Order.dianpu.ToUpper() == "BONTON")
					{
						return false;
					}
					//1.不是AB包，也不应该是卡车单
					//2.不是店铺独立拣货的
					//3.仓库启用传送带功能(主仓和自仓需要分别判断) & s判断way类型是否允许发传送带
					//4.如果有体积条件，则需要满足体积条件,没有则所有都满足
					//5.排除在名单里面的内容

					#region 不是AB包，也不应该是卡车单
					bool IsFilter = false;
					var sheets = db.scb_xsjlsheet.Where(s => s.father == Order.number).ToList();
					var sheet = sheets.FirstOrDefault();
					if (sheets.Count() == 1)
					{
						if (sheets.FirstOrDefault().sl > 1)
							IsFilter = true;
						else
						{
							sheet = sheets.FirstOrDefault();
							if (db.Transportation.Where(t => t.ItemNo == sheet.cpbh || t.ItemNo == sheet.sku).ToList().Any())
								IsFilter = true;
						}
					}
					else
					{
						IsFilter = true;
					}

					if (IsFilter)
					{
						Helper.Info(number, "wms_api", $"{Order.number}:{sheet.cpbh}卡车运输或者ab包裹,不支持传送带");
						return false;
					}
					#endregion

					#region 不是店铺独立拣货的
					if (Order.dianpu.ToUpper() == "GOVENDOR-W" || Order.dianpu.ToUpper() == "GXVENDOR-W")
					{
						Helper.Info(number, "wms_api", $"dianpu{Order.dianpu}店铺独立拣货,不支持传送带");
						return false;
					}
					#endregion



					#region  体积条件，则需要满足体积条件,没有则所有都满足

					var goods = db.N_goods.Where(g => g.country == Order.country && g.cpbh == sheet.cpbh).FirstOrDefault();
					var realsize = db.scb_realsize.Where(g => g.country == Order.country && g.cpbh == sheet.cpbh).FirstOrDefault();
					//磅
					var Weight = goods.weight_express.Value * sheet.sl / 1000 * double.Parse("2.2046226");
					var packageLengtht = realsize.bzcd.Value;
					var packageHeight = realsize.bzgd.Value;
					var packageWidth = realsize.bzkd.Value;
					var sizeList = new List<double>();
					sizeList.Add(packageLengtht);
					sizeList.Add(packageHeight);
					sizeList.Add(packageWidth);
					if (Weight < 1 || Weight > 75)
					{
						Helper.Info(number, "wms_api", $"仓库{warehouseID},{cpbh},重量{Weight}超标。不支持传送带");
						return false;
					}
					var n1 = sizeList.OrderByDescending(o => o).ElementAt(0);
					var n2 = sizeList.OrderByDescending(o => o).ElementAt(1);
					var n3 = sizeList.OrderByDescending(o => o).ElementAt(2);
					if (n1 < 10 || n1 > 60)
					{
						Helper.Info(number, "wms_api", $"仓库{warehouseID},{cpbh},长{n1}超标。不支持传送带");
						return false;
					}
					if (n2 < 6 || n2 > 32)
					{
						Helper.Info(number, "wms_api", $"仓库{warehouseID},{cpbh},宽{n2}超标。不支持传送带");
						return false;
					}
					if (n3 < 3 || n3 > 30)
					{
						Helper.Info(number, "wms_api", $"仓库{warehouseID},{cpbh},高{n3}超标。不支持传送带");
						return false;
					}

					#endregion

					#region 排除在名单里面的内容
					var blackList = db.ods_wmsus_Conveyer_Black.Where(o => o.Enable && o.ItemNumber == cpbh && warehouseID == warehouseID).ToList();
					if (blackList.Any())
					{
						Helper.Info(number, "wms_api", $"仓库{warehouseID},{cpbh}在黑名单中，不支持传送带");
						return false;
					}
					#endregion
					return true;
				}
				Helper.Info(number, "wms_api", "找不到该订单");
				return false;

			}
			catch (Exception ex)
			{
				LogHelper.WriteLog("CanSendByZPL报错", ex);
				return false;
			}
		}

		public class ScbckModel
		{
			public int number { get; set; }
			public string id { get; set; }
			public int? firstsend { get; set; }
			public string AreaLocation { get; set; }
			public int? AreaSort { get; set; }
			public string Origin { get; set; }
			public int? OriginSort { get; set; }
			public int? IsMatching { get; set; }
			public int? IsThird { get; set; }
			public string oids { get; set; }

			public int Sort { get; set; }
			public int Levels { get; set; }
			public int EffectiveInventory { get; set; }
			public string realname { get; set; }
			public int? GNDZone { get; set; }
		}

		#endregion

		#region 仓库匹配
		object MatchWarehouseLock = new object();
		//object LoadDataIntoMemoryLock = new object();

		public MatchController()
		{
			if (_cacheZip == null)
			{
				//_cacheZip = LoadDataIntoMemory();
				_cacheZip = DataCache.GetAllData();
			}
			if (_cacheOntracZip == null)
			{
				//_cacheZip = LoadDataIntoMemory();
				_cacheOntracZip = DataCache.GetOntracAllData();
			}
			if (_cacheFimileZip == null)
			{
				//_cacheZip = LoadDataIntoMemory();
				_cacheFimileZip = DataCache.GetFimileAllData();
			}
			if (_cacheGOFOZip == null)
			{
				//_cacheZip = LoadDataIntoMemory();
				_cacheGOFOZip = DataCache.GetGOFOAllData();
			}
			if (_cacheWesternPostZip == null)
			{
				_cacheWesternPostZip = DataCache.GetWesternPostAllData();
			}
			if (_cacheXmileZip == null)
			{
				//_cacheZip = LoadDataIntoMemory();
				_cacheXmileZip = DataCache.GetXmileAllData();
			}
			if (_cacheRoadieZip == null)
			{
				//_cacheZip = LoadDataIntoMemory();
				_cacheRoadieZip = DataCache.GetRoadieAllData();
			}
			//if (_UPSSkuList == null)
			//{
			//	_UPSSkuList = LoadUPSSKusIntoMemory();
			//}
			if (_ThirdBillAccountList == null)
			{
				_ThirdBillAccountList = LoadThirdBillAccountIntoMemory();
			}
			if (_ThirdBillAccountList == null)
			{
				_ThirdBillAccountList = LoadThirdBillAccountIntoMemory();
			}
		}

		private List<string> LoadUPSSKusIntoMemory()
		{
			using (var db = new cxtradeModel())
			{
				List<string> data = db.Database.SqlQuery<string>("select Parameter from scb_WMS_MatchRule_Details where rid in (select id from scb_WMS_MatchRule where type = 'Only_UPSGround_Send') and type = 'ItemNo'").ToList();

				return data;
			}
		}

		private List<scb_third_party> LoadThirdBillAccountIntoMemory()
		{
			using (var db = new cxtradeModel())
			{
				List<scb_third_party> data = db.scb_third_party.AsNoTracking().ToList();

				return data;
			}
		}

		/// <summary>
		/// 欧洲新匹配-20200624
		/// </summary>
		/// <param name="Order"></param>
		/// <param name="OrderMatch"></param>
		private void MatchWarehouseByEU_V2(scb_xsjl Order, ref OrderMatchResult OrderMatch)
		{
			var db = new cxtradeModel();
			var Tocountry = Order.temp10;
			var datformat = DateTime.Now.ToString("yyyy-MM-dd");

			List<scbck> Warehouses = db.scbck.Where(c => c.countryid == "05" && c.state == 1).ToList();
			var MatchRules = db.scb_WMS_MatchRule.Where(p => p.Country == "EU" && p.State == 1).ToList();
			var sheets = db.scb_xsjlsheet.Where(f => f.father == Order.number).ToList();

			#region  黑名单
			var blackEmail = new List<string>();
			//2023-09-18 丁佳 店铺FDVAM - PL 邮箱地址mxpzbfy36kg78kq@marketplace.amazon.pl，打单拦截一下哦（自动和手动都拦截）
			blackEmail = "mxpzbfy36kg78kq@marketplace.amazon.pl|xt45fqylspgp75r@marketplace.amazon.de|rs70phf70vqyc6z@marketplace.amazon.de|6jyrbf50bpqggyw@marketplace.amazon.de|rbmy3vknxw7bddh@marketplace.amazon.de|58q8v5j5bgfgmcx@marketplace.amazon.de|s1kgtfj62zymskj@marketplace.amazon.de|wf9d6bbn659hwy0@marketplace.amazon.de|dccys7mh9kxhfhd@marketplace.amazon.de|7fcjbn99wv2f86r@marketplace.amazon.de|y4nnldkmfmhdgjz@marketplace.amazon.de|2bkt15tpd1xbd25@marketplace.amazon.de|084h3g6tp4gwfx0@marketplace.amazon.de|yfjw18l0kj4vv7d@marketplace.amazon.de|ddsvhsxrj52wkj2@marketplace.amazon.de|pfgkxnsl8zcghqz@marketplace.amazon.de|zvj9y9ny5g9fw0v@marketplace.amazon.de".Split('|').ToList();
			if (blackEmail.Contains(Order.email))
			{
				OrderMatch.Result.Message = "黑名单，请检查后手工判断";
				return;
			}
			#region 20240516 邵露露 地址黑名单 邮编23041  或  地址 城市  州出现 livigno 全欧盟都禁止
			if (Order.zip == "23041" || Order.adress.ToLower().Contains("livigno") || Order.address1.ToLower().Contains("livigno") || Order.city.ToLower().Contains("livigno") || Order.statsa.ToLower().Contains("livigno"))
			{
				OrderMatch.Result.Message = " livigno  黑名单发不了";
				return;
			}
			//20240624 邵露露 Liz y Manuel或者Miguel y Liz，Carrera Plana 39,maria de la salut, Islas Baleares,07519 加黑名单
			if ((Order.khname == "Liz y Manuel" || Order.khname == "Miguel y Liz") && Order.adress == "Carrera Plana 39")
			{
				OrderMatch.Result.Message = " Carrera Plana 39 黑名单发不了";
				return;
			}

			var blackRule = MatchRules.FirstOrDefault(p => p.Type == "Blacklist");
			if (blackRule != null)
			{
				var blackRule_detail = db.scb_WMS_MatchRule_Details.Where(p => p.RID == blackRule.ID && p.Type == "Zip").ToList();
				if (blackRule_detail.Any(p => p.Parameter == Order.zip))
				{
					OrderMatch.Result.Message = "邮编黑名单发不了";
					return;
				}
			}

			#endregion

			//20240627 冯慧慧 FDVAM-FR   NP11572DK-2=ZB33794PW-4FR	这个货号设置下拦截， 不要发
			var black_cpbhs = new List<string>() { "NP11572DK-2", "ZB33794PW-4FR" };
			if (Order.dianpu == "FDVAM-FR" && sheets.Any(p => black_cpbhs.Any(o => o == p.cpbh)))
			{
				OrderMatch.Result.Message = $"FDVAM-FR 禁售 NP11572DK-2,ZB33794PW-4FR 货号，请取消!";
				return;
			}

			#endregion

			#region openbox
			if (!string.IsNullOrEmpty(Order.chck))
			{
				if (Order.chck.ToLower().Contains("open"))
				{

					var CurrentItemNo = sheets.FirstOrDefault().cpbh;
					var Checkeds = db.Database.SqlQuery<OrderMatchEntity>(string.Format(
						@"select c.number WarehouseID, c.id WarehouseName,c.Origin WarehouseCountry,d.logisticsservicemode SericeType,d.logisticsid Logicswayoid,d.fee Fee from scbck c inner join OSV_EU_LogisticsForProduct d on
                            c.groupname=d.warehouse and d.goods='{0}' and IsChecked=1 and signtouse=0  and country='{1}' 
                            where d.fee>0 ", CurrentItemNo, Order.temp10));  //and standby=0
					var FromWareHouse = new List<string>();
					List<OrderMatchEntity> w = new List<OrderMatchEntity>();
					//找到能发的物流仓库列表
					switch (Order.temp10.ToUpper())
					{
						case "PL":
							FromWareHouse = "EU70".Split(',').ToList();
							w = Checkeds.Where(o => o.WarehouseName == "EU70" && (o.Logicswayoid == 19 || o.Logicswayoid == 31)).ToList();
							break;
						case "IT":
							FromWareHouse = "EU70,EU87".Split(',').ToList();
							w = Checkeds.Where(o => (o.WarehouseName == "EU70" && o.Logicswayoid == 71) || (o.WarehouseName == "EU87" && o.Logicswayoid == 31)).ToList();
							break;
						case "FR":
							FromWareHouse = "EU72".Split(',').ToList();
							w = Checkeds.Where(o => o.WarehouseName == "EU72" && (o.Logicswayoid == 19 || o.Logicswayoid == 31 || o.Logicswayoid == 73 || o.Logicswayoid == 72)).ToList();
							break;
						case "DE":
							FromWareHouse = "EU92,EU96,EU72,EU70".Split(',').ToList();
							w = Checkeds.Where(o =>
							   (o.WarehouseName == "EU92" && (o.Logicswayoid == 20 || o.Logicswayoid == 31))
							|| (o.WarehouseName == "EU96" && (o.Logicswayoid == 20 || o.Logicswayoid == 31))
							|| (o.WarehouseName == "EU72" && (o.Logicswayoid == 73 || o.Logicswayoid == 72))
							|| (o.WarehouseName == "EU70" && (o.Logicswayoid == 20 || o.Logicswayoid == 65))).ToList();
							break;
					}
					Helper.Info(Order.number, "wms_api", JsonConvert.SerializeObject(w));
					//判断库存
					var kcsl = db.Database.SqlQuery<WarehouseKCSLDetail>($"select count(1) Qty,Location AS WarehouseNo from scb_kcjl_area_cangw where  sl>0 and cpbh='{CurrentItemNo}' AND Location IN ({string.Join(",", FromWareHouse.Select(o => "'" + o + "'"))} ) GROUP BY Location");
					Helper.Info(Order.number, "wms_api", JsonConvert.SerializeObject(w));
					if (!kcsl.Any())
					{
						OrderMatch.Result.Message = "匹配错误,库存不足";
					}
					else
					{
						Helper.Info(Order.number, "wms_api", JsonConvert.SerializeObject(kcsl));
						var t1 = kcsl.Select(o => o.WarehouseNo).ToList();
						var Open_Warehouse = w.Where(o => t1.Contains(o.WarehouseName)).OrderBy(o => o.Fee).FirstOrDefault();
						if (Open_Warehouse == null)
						{
							OrderMatch.Result.Message = "匹配错误,库存不足";
						}
						else
						{
							OrderMatch.WarehouseID = Open_Warehouse.WarehouseID;
							OrderMatch.WarehouseName = Open_Warehouse.WarehouseName;
							OrderMatch.Logicswayoid = Open_Warehouse.Logicswayoid;
							OrderMatch.SericeType = Open_Warehouse.SericeType;
						}
					}
					return;





					//var Open_Warehouse = Warehouses.FirstOrDefault(w => w.AreaLocation.ToLower() == Order.chck.ToLower());
					//if (Open_Warehouse != null)
					//{
					//    var CurrentItemNo = sheets.FirstOrDefault().cpbh;
					//    var Checkeds = db.Database.SqlQuery<OrderMatchEntity>(string.Format(
					//        @"select c.number WarehouseID, c.id WarehouseName,c.Origin WarehouseCountry,d.logisticsservicemode SericeType,d.logisticsid Logicswayoid,d.fee Fee from scbck c inner join scb_logisticsforproduct_new d on
					//        c.groupname=d.warehouse and d.goods='{0}' and IsChecked=1 and signtouse=0 and standby=0 and country='{1}' 
					//        where c.number={2} and d.fee>0 ", CurrentItemNo, Order.temp10, Open_Warehouse.number));
					//    if (Checkeds.Any())
					//    {
					//        var Checked = Checkeds.OrderBy(f => f.Fee).FirstOrDefault();
					//        OrderMatch.WarehouseID = Checked.WarehouseID;
					//        OrderMatch.WarehouseName = Checked.WarehouseName;
					//        OrderMatch.Logicswayoid = Checked.Logicswayoid;
					//        OrderMatch.SericeType = Checked.SericeType;



					//        if (Open_Warehouse.id == "EU96")
					//        {
					//            var stock = db.Database.SqlQuery<int>(string.Format(
					//                @"select count(1) from scb_kcjl_area_cangw where Location='EU96' and sl>0 and cpbh='{0}'", CurrentItemNo)).ToList().First();

					//            if (stock == 0)
					//            {
					//                Open_Warehouse = Warehouses.FirstOrDefault(w => w.id == "EU92");
					//                OrderMatch.WarehouseID = Open_Warehouse.number;
					//                OrderMatch.WarehouseName = Open_Warehouse.id;
					//            }
					//        }
					//        return;
					//    }
					//}
				}
			}
			#endregion

			#region 常规匹配

			var ws = new Dictionary<decimal, List<OrderMatchEntity>>();
			var message = string.Empty;

			foreach (var item in sheets)
			{
				System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace(1, true);
				Stopwatch sw = new Stopwatch();
				sw.Start();

				var CurrentItemNo = item.cpbh;
				//具体哪个仓库发的价格和物流多少钱 SELECT * FROM dbo.scb_logisticsForproduct_new WHERE Goods='SP35692BL'  AND  IsChecked=1 and signtouse=0 and standby=0 
				//scb_WMS_Pond 统计表
				var Sql = string.Format(@"select kcsl,unshipped,unshipped2,c.number WarehouseID,c.id WarehouseName,c.Origin WarehouseCountry,d.logisticsservicemode SericeType,d.logisticsid Logicswayoid,d.fee Fee,d.fee OldFee
,case when c.Origin ='{3}' then 1 else 0 end islocalWare
from (
select Warehouse, SUM(sl) kcsl,
isnull((select SUM(Qty) from scb_WMS_Pond where state=0 and Warehouse =a.Warehouse and ItemNo = '{0}'), 0) unshipped2,
ISNULL((select sum(s.sl) from scb_xsjl x inner join scb_xsjlsheet s on x.number=s.father
left join scbck c on x.warehouseid=c.number
left join scb_Logisticsmode d on x.logicswayoid=d.oid
where country='EU' and x.state=0 and filterflag>=7 and filterflag<=10 and not exists(select 1 from scb_WMS_Pond where state=0 and NID=x.number)
and isnull(ismark,'')=0 and x.temp3 not in ('4','7') and s.cpbh='{0}' and c.id=a.Warehouse),0) unshipped
from scb_kcjl_location a inner join scb_kcjl_area_cangw b
on a.Warehouse = b.Location and a.Location = b.cangw
where b.state = 0 and a.Disable = 0  and a.LocationType<10 and b.cpbh = '{0}'
group by Warehouse) t inner join scbck c on
t.Warehouse=c.name inner join OSV_EU_LogisticsForProduct d 
on c.groupname=d.warehouse and IsChecked=1 and signtouse=0 and country='{3}' and d.goods='{0}'
and d.fee>0
where countryid='05' and state=1 and c.IsMatching=1 and c.realname not like 'open%' and c.realname not like 'fb%' and kcsl-unshipped - unshipped2 >= {1}", CurrentItemNo, item.sl, datformat, Order.temp10);
				var Checkeds = db.Database.SqlQuery<OrderMatchEntity>(Sql).ToList();


				sw.Stop();
				//获取运行时间[毫秒]  
				Helper.Monitor(sw.ElapsedMilliseconds, Order.number, $"源码行数：{st.GetFrame(0).GetFileLineNumber()} 执行查库存物流的sql");
				sw.Restart();

				Helper.Info(Order.number, "wms_api", JsonConvert.SerializeObject(Checkeds));
				ws.Add(item.number, FilterByLogistics(Order, Checkeds, sheets, item, ref message));
				OrderMatch.Result.Message += message;

				sw.Stop();
				Helper.Monitor(sw.ElapsedMilliseconds, Order.number, $"源码行数：{st.GetFrame(0).GetFileLineNumber()} FilterByLogistics");
			}

			if (ws.Where(p => p.Value.Count > 0).Any())
			{
				var Baselist = ws.FirstOrDefault().Value;
				if (ws.Count > 1)
				{
					foreach (var item in ws)
					{
						Baselist = Baselist.Where(f => item.Value.Any(g => g.WarehouseID == f.WarehouseID)).ToList();
					}
				}
				if (Baselist.Any())
				{
					MatchActualWarehouseEU(sheets, Baselist, ref OrderMatch);
				}
				else
					OrderMatch.Result.Message = "错误,N个不同的仓库，需要分拆订单";

				#region 注释
				//if (ws.Count() == 1)
				//{
				//	var CurrentWarehouse = ws.FirstOrDefault().Value.OrderBy(f => f.Fee).ThenByDescending(f => f.WarehouseID).FirstOrDefault();
				//	if (CurrentWarehouse == null)
				//	{
				//		OrderMatch.Result.Message = string.IsNullOrEmpty(OrderMatch.Result.Message) ? "匹配错误,库存不足" : OrderMatch.Result.Message;
				//	}
				//	else
				//	{
				//		OrderMatch.WarehouseID = CurrentWarehouse.WarehouseID;
				//		OrderMatch.WarehouseName = CurrentWarehouse.WarehouseName;
				//		OrderMatch.Logicswayoid = CurrentWarehouse.Logicswayoid;
				//		OrderMatch.SericeType = CurrentWarehouse.SericeType;
				//	}
				//}
				//else
				//{
				//	var Baselist = ws.FirstOrDefault().Value;
				//	foreach (var item in ws)
				//	{
				//		Baselist = Baselist.Where(f => item.Value.Any(g => g.WarehouseID == f.WarehouseID)).ToList();
				//	}
				//	if (Baselist.Any())
				//	{
				//		var CurrentWarehouse = Baselist.OrderBy(f => f.Fee).ThenByDescending(f => f.WarehouseID).FirstOrDefault();
				//		OrderMatch.WarehouseID = CurrentWarehouse.WarehouseID;
				//		OrderMatch.WarehouseName = CurrentWarehouse.WarehouseName;
				//		OrderMatch.Logicswayoid = CurrentWarehouse.Logicswayoid;
				//		OrderMatch.SericeType = CurrentWarehouse.SericeType;
				//	}
				//	else
				//		OrderMatch.Result.Message = "错误,N个不同的仓库，需要分拆订单";
				//}
				#endregion
			}
			else
			{
				OrderMatch.Result.Message = string.IsNullOrEmpty(OrderMatch.Result.Message) ? "匹配错误,库存不足" : OrderMatch.Result.Message;
			}

			#endregion

		}

		/// <summary>
		/// 欧洲新匹配 去xsjl表-20241021
		/// </summary>
		/// <param name="Order"></param>
		/// <param name="OrderMatch"></param>
		private void MatchWarehouseByEU_V3(EUMatchModel model, ref OrderMatchResult OrderMatch)
		{
			var db = new cxtradeModel();
			var Tocountry = model.temp10;
			var datformat = DateTime.Now.ToString("yyyy-MM-dd");

			List<scbck> Warehouses = db.scbck.Where(c => c.countryid == "05" && c.state == 1).ToList();
			var MatchRules = db.scb_WMS_MatchRule.Where(p => p.Country == "EU" && p.State == 1).ToList();
			//var sheets = db.scb_xsjlsheet.Where(f => f.father == model.number).ToList();

			#region  黑名单
			var blackEmail = new List<string>();
			//2023-09-18 丁佳 店铺FDVAM - PL 邮箱地址mxpzbfy36kg78kq@marketplace.amazon.pl，打单拦截一下哦（自动和手动都拦截）
			blackEmail = "mxpzbfy36kg78kq@marketplace.amazon.pl|xt45fqylspgp75r@marketplace.amazon.de|rs70phf70vqyc6z@marketplace.amazon.de|6jyrbf50bpqggyw@marketplace.amazon.de|rbmy3vknxw7bddh@marketplace.amazon.de|58q8v5j5bgfgmcx@marketplace.amazon.de|s1kgtfj62zymskj@marketplace.amazon.de|wf9d6bbn659hwy0@marketplace.amazon.de|dccys7mh9kxhfhd@marketplace.amazon.de|7fcjbn99wv2f86r@marketplace.amazon.de|y4nnldkmfmhdgjz@marketplace.amazon.de|2bkt15tpd1xbd25@marketplace.amazon.de|084h3g6tp4gwfx0@marketplace.amazon.de|yfjw18l0kj4vv7d@marketplace.amazon.de|ddsvhsxrj52wkj2@marketplace.amazon.de|pfgkxnsl8zcghqz@marketplace.amazon.de|zvj9y9ny5g9fw0v@marketplace.amazon.de".Split('|').ToList();
			if (blackEmail.Contains(model.email))
			{
				OrderMatch.Result.Message = "黑名单，请检查后手工判断";
				return;
			}
			//2025-04-25 冯慧慧 这个3邮箱是骗子manomano-es店铺加黑名单
			var blackEmailMano = new List<string>();
			blackEmailMano = "m_cjieibhf3iabdejjd2gdjhhhd11@message.manomano.com|m_DEJFGACH3HEGJGIGF2GDJHHHD11@message.manomano.com|m_defjeebj3hjfiibhc2gdjhhhd11@message.manomano.com".Split('|').ToList();
			if (blackEmailMano.Contains(model.email) && model.dianpu.ToLower() == "manomano-es")
			{
				OrderMatch.Result.Message = "黑名单，请检查后手工判断";
				return;
			}

			//20250619 冯慧慧 这个顾客不要派送，骗子
			if (model.khname.ToLower() == "simona delli santi" || model.phone == "3206858496")
			{
				OrderMatch.Result.Message = $"收件人:simona delli santi，电话:3206858496 黑名单，请检查后手工判断";
				return;
			}

			#region 20240516 邵露露 地址黑名单 邮编23041  或  地址 城市  州出现 livigno 全欧盟都禁止
			if (model.zip == "23041" || model.adress.ToLower().Contains("livigno") || model.address1.ToLower().Contains("livigno") || model.city.ToLower().Contains("livigno") || model.statsa.ToLower().Contains("livigno"))
			{
				OrderMatch.Result.Message = " livigno  黑名单发不了";
				return;
			}

			//20240624 邵露露 Liz y Manuel或者Miguel y Liz，Carrera Plana 39,maria de la salut, Islas Baleares,07519 加黑名单
			if ((model.khname == "Liz y Manuel" || model.khname == "Miguel y Liz") && model.adress == "Carrera Plana 39")
			{
				OrderMatch.Result.Message = " Carrera Plana 39 黑名单发不了";
				return;
			}
			//20250613 兰地花 名字 Mariella  Dominici 58017 Pitigliano  加黑名单
			if ((model.city.ToLower().Contains("pitigliano") && model.zip == "58017" && model.adress.ToLower().Contains("73b")) || model.khname.ToLower().Contains("mariella  dominici"))
			{
				OrderMatch.Result.Message = " mariella  dominici 客户 黑名单发不了";
				return;
			}
			var blackRule = MatchRules.FirstOrDefault(p => p.Type == "Blacklistzip");
			if (blackRule != null)
			{
				if (db.scb_WMS_MatchRule_Details.Any(p => p.RID == blackRule.ID && p.Parameter == model.zip && (p.Type == model.temp10 || p.Type == "")))
				{
					OrderMatch.Result.Message = "邮编黑名单发不了";
					return;
				}
			}

			//20250812 邵露露
			if ((model.zip == "37006" && model.adress.Contains("San Bruno 62") && model.city.ToLower() == "salamanca") || (model.zip == "37001" && model.adress.Contains("calle santa clara 5 ático") && model.city.ToLower() == "salamanca"))
			{
				OrderMatch.Result.Message = "骗子拦截";
				return;
			}

			#endregion

			//20240627 冯慧慧 FDVAM-FR   NP11572DK-2=ZB33794PW-4FR	这个货号设置下拦截， 不要发
			var black_cpbhs = new List<string>() { "NP11572DK-2", "ZB33794PW-4FR" };
			if (model.dianpu == "FDVAM-FR" && model.sheets.Any(p => black_cpbhs.Any(o => o == p.cpbh)))
			{
				OrderMatch.Result.Message = $"FDVAM-FR 禁售 NP11572DK-2,ZB33794PW-4FR 货号，请取消!";
				return;
			}

			#region  20241223 冯慧慧 西班牙 加那利群岛 限制，基本发不了   20250211 冯慧慧 35550	 加那利群岛的邮编 麻烦设置下拦截哦, 20250505 陈赛芬  38205是西班牙加那利群岛 麻烦添加下拦截哦
			if (model.temp10 == "ES")
			{
				var tmp_blackRule = MatchRules.FirstOrDefault(p => p.Type == "blacklist_CanaryIslands");
				var tmp_blackRuleDetails = db.scb_WMS_MatchRule_Details.Where(p => p.RID == tmp_blackRule.ID).ToList();
				if (tmp_blackRuleDetails.Any(p =>
				(p.Type == "city" && p.Parameter.ToLower() == model.city.ToLower())
				|| (p.Type == "zip" && p.Parameter == model.zip)
				|| (p.Type == "address" && (model.adress.ToLower().Contains(p.Parameter.ToLower()) || model.address1.ToLower().Contains(p.Parameter.ToLower())))
				|| (p.Type == "statsa" && p.Parameter.ToLower() == model.statsa.ToLower())
				))
				{
					OrderMatch.Result.Message = $"西班牙，加那利群岛 限制！";
					return;
				}

				//var ban_EScity = "Moya,La Vizcaína,Casillas de Morales,Las Palmas de Gran Canaria,Puerto del Rosario,San Bartolomé de Tirajana,Playa de la Américas,La Pared".Split(',').ToList();
				//if (ban_EScity.Any(p => p.ToLower() == model.city.ToLower())
				//	|| model.statsa.ToLower() == "las palmas"
				//	|| model.adress.Contains("Gran Canaria") || model.address1.Contains("Gran Canaria")
				//	|| model.adress.Contains("Maspalomas") || model.address1.Contains("Maspalomas")
				//	|| model.zip == "35550" || model.zip == "38205" || model.zip == "35625" || model.zip == "38350" || model.zip == "38530" || model.zip == "38760"
				//)
				//{
				//	OrderMatch.Result.Message = $"西班牙，加那利群岛 限制！";
				//	return;
				//}
			}

			#endregion

			if (BlackAddress(model.dianpu, model.xspt, model.khname, model.adress, model.address1, model.city, "EU", model.email, model.fkzh, model.phone, model.bz))
			{
				OrderMatch.Result.Message = $"退单率高，Hold单，请确认是否发货！";
				return;
			}
			#endregion

			#region openbox
			if (!string.IsNullOrEmpty(model.chck))
			{
				if (model.chck.ToLower().Contains("open"))
				{

					var CurrentItemNo = model.sheets.FirstOrDefault().cpbh;
					var Checkeds = db.Database.SqlQuery<OrderMatchEntity>(string.Format(
						@"select c.number WarehouseID, c.id WarehouseName,c.Origin WarehouseCountry,d.logisticsservicemode SericeType,d.logisticsid Logicswayoid,d.fee Fee from scbck c inner join OSV_EU_LogisticsForProduct d on
                            c.groupname=d.warehouse and d.goods='{0}' and IsChecked=1 and signtouse=0  and country='{1}' 
                            where d.fee>0 ", CurrentItemNo, model.temp10));  //and standby=0
					var FromWareHouse = new List<string>();
					List<OrderMatchEntity> w = new List<OrderMatchEntity>();
					//找到能发的物流仓库列表
					switch (model.temp10.ToUpper())
					{
						case "PL":
							FromWareHouse = "EU70".Split(',').ToList();
							w = Checkeds.Where(o => o.WarehouseName == "EU70" && (o.Logicswayoid == 19 || o.Logicswayoid == 31)).ToList();
							break;
						case "IT":
							FromWareHouse = "EU70,EU87".Split(',').ToList();
							w = Checkeds.Where(o => (o.WarehouseName == "EU70" && o.Logicswayoid == 71) || (o.WarehouseName == "EU87" && o.Logicswayoid == 31)).ToList();
							break;
						case "FR":
							FromWareHouse = "EU72".Split(',').ToList();
							w = Checkeds.Where(o => o.WarehouseName == "EU72" && (o.Logicswayoid == 19 || o.Logicswayoid == 31 || o.Logicswayoid == 73 || o.Logicswayoid == 72)).ToList();
							break;
						case "DE":
							FromWareHouse = "EU92,EU96,EU72,EU70".Split(',').ToList();
							w = Checkeds.Where(o =>
							   (o.WarehouseName == "EU92" && (o.Logicswayoid == 20 || o.Logicswayoid == 31))
							|| (o.WarehouseName == "EU96" && (o.Logicswayoid == 20 || o.Logicswayoid == 31))
							|| (o.WarehouseName == "EU72" && (o.Logicswayoid == 73 || o.Logicswayoid == 72))
							|| (o.WarehouseName == "EU70" && (o.Logicswayoid == 20 || o.Logicswayoid == 65))).ToList();
							break;
					}
					Helper.Info(model.number, "wms_api", JsonConvert.SerializeObject(w));
					//判断库存
					var kcsl = db.Database.SqlQuery<WarehouseKCSLDetail>($"select count(1) Qty,Location AS WarehouseNo from scb_kcjl_area_cangw where  sl>0 and cpbh='{CurrentItemNo}' AND Location IN ({string.Join(",", FromWareHouse.Select(o => "'" + o + "'"))} ) GROUP BY Location");
					Helper.Info(model.number, "wms_api", JsonConvert.SerializeObject(w));
					if (!kcsl.Any())
					{
						OrderMatch.Result.Message = "匹配错误,库存不足";
					}
					else
					{
						Helper.Info(model.number, "wms_api", JsonConvert.SerializeObject(kcsl));
						var t1 = kcsl.Select(o => o.WarehouseNo).ToList();
						var Open_Warehouse = w.Where(o => t1.Contains(o.WarehouseName)).OrderBy(o => o.Fee).FirstOrDefault();
						if (Open_Warehouse == null)
						{
							OrderMatch.Result.Message = "匹配错误,库存不足";
						}
						else
						{
							OrderMatch.WarehouseID = Open_Warehouse.WarehouseID;
							OrderMatch.WarehouseName = Open_Warehouse.WarehouseName;
							OrderMatch.Logicswayoid = Open_Warehouse.Logicswayoid;
							OrderMatch.SericeType = Open_Warehouse.SericeType;
						}
					}
					return;





					//var Open_Warehouse = Warehouses.FirstOrDefault(w => w.AreaLocation.ToLower() == Order.chck.ToLower());
					//if (Open_Warehouse != null)
					//{
					//    var CurrentItemNo = sheets.FirstOrDefault().cpbh;
					//    var Checkeds = db.Database.SqlQuery<OrderMatchEntity>(string.Format(
					//        @"select c.number WarehouseID, c.id WarehouseName,c.Origin WarehouseCountry,d.logisticsservicemode SericeType,d.logisticsid Logicswayoid,d.fee Fee from scbck c inner join scb_logisticsforproduct_new d on
					//        c.groupname=d.warehouse and d.goods='{0}' and IsChecked=1 and signtouse=0 and standby=0 and country='{1}' 
					//        where c.number={2} and d.fee>0 ", CurrentItemNo, Order.temp10, Open_Warehouse.number));
					//    if (Checkeds.Any())
					//    {
					//        var Checked = Checkeds.OrderBy(f => f.Fee).FirstOrDefault();
					//        OrderMatch.WarehouseID = Checked.WarehouseID;
					//        OrderMatch.WarehouseName = Checked.WarehouseName;
					//        OrderMatch.Logicswayoid = Checked.Logicswayoid;
					//        OrderMatch.SericeType = Checked.SericeType;



					//        if (Open_Warehouse.id == "EU96")
					//        {
					//            var stock = db.Database.SqlQuery<int>(string.Format(
					//                @"select count(1) from scb_kcjl_area_cangw where Location='EU96' and sl>0 and cpbh='{0}'", CurrentItemNo)).ToList().First();

					//            if (stock == 0)
					//            {
					//                Open_Warehouse = Warehouses.FirstOrDefault(w => w.id == "EU92");
					//                OrderMatch.WarehouseID = Open_Warehouse.number;
					//                OrderMatch.WarehouseName = Open_Warehouse.id;
					//            }
					//        }
					//        return;
					//    }
					//}
				}
			}
			#endregion

			#region 常规匹配

			if (model.sheets.Any(p => p.sl > 3) || model.sheets.Count > 1)
			{
				OrderMatch.Result.Message = "暂不支持多包，请拆单，或卡车";
				return;
			}

			var ws = new Dictionary<decimal, List<OrderMatchEntity>>();
			var message = string.Empty;

			foreach (var item in model.sheets)
			{
				System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace(1, true);
				Stopwatch sw = new Stopwatch();
				sw.Start();

				var CurrentItemNo = item.cpbh;
				//具体哪个仓库发的价格和物流多少钱 SELECT * FROM dbo.scb_logisticsForproduct_new WHERE Goods='SP35692BL'  AND  IsChecked=1 and signtouse=0 and standby=0 
				//scb_WMS_Pond 统计表
				var Sql = string.Format(@"select kcsl,unshipped,unshipped2,occupation,c.number WarehouseID,c.Origin,c.id WarehouseName,c.Origin WarehouseCountry,d.logisticsservicemode SericeType,d.logisticsid Logicswayoid,d.fee Fee,d.fee OldFee,d.Fee_Box2,d.Fee_Box3 
,case when c.Origin ='{3}' then 1 else 0 end islocalWare
from (
select Warehouse, SUM(sl) kcsl,
isnull((select SUM(Qty) from scb_WMS_Pond where state=0 and Warehouse =a.Warehouse and ItemNo = '{0}'), 0) unshipped2,
ISNULL((select sum(s.sl) from scb_xsjl x inner join scb_xsjlsheet s on x.number=s.father
left join scbck c on x.warehouseid=c.number
left join scb_Logisticsmode d on x.logicswayoid=d.oid
where country='EU' and x.state=0 and filterflag>=7 and filterflag<=10 and not exists(select 1 from scb_WMS_Pond where state=0 and NID=x.number)
and isnull(ismark,'')=0 and x.temp3 not in ('4','7') and s.cpbh='{0}' and c.id=a.Warehouse),0) unshipped,
isnull((select SUM(qty)  from ods_wmseu_Stock_PreOccupationSheet where WarehouseId = Warehouse and ItemNumber = '{0}' and Status=0 and IsValid =1), 0) occupation
from scb_kcjl_location a inner join scb_kcjl_area_cangw b
on a.Warehouse = b.Location and a.Location = b.cangw
where b.state = 0 and a.Disable = 0  and a.LocationType<10 and b.cpbh = '{0}'
group by Warehouse) t inner join scbck c on
t.Warehouse=c.name inner join OSV_EU_LogisticsForProduct d 
on c.groupname=d.warehouse and IsChecked=1 and signtouse=0 and country='{3}' and d.goods='{0}'
and d.fee>0
where countryid='05' and state=1 and c.IsMatching=1 and c.realname not like 'open%' and c.realname not like 'fb%' and kcsl-unshipped - unshipped2 -occupation >= {1}", CurrentItemNo, item.sl, datformat, model.temp10);
				var Checkeds = db.Database.SqlQuery<OrderMatchEntity>(Sql).ToList();


				#region  20250508 兰地花 ，allegro，allegro-costway，goplus-allegro  如果指定DPD，可以发PL的DPD
				var canDPD_dianpus = "allegro,allegro-costway,goplus-allegro".Split(',').ToList();
				if (canDPD_dianpus.Any(p => p.ToLower() == model.dianpu.ToLower()) && !Checkeds.Any(p => p.SericeType == "DPD" && p.WarehouseCountry == "PL"))
				{
					var ignoreShipRuleSql = string.Format(@"select kcsl,unshipped,unshipped2,occupation,c.number WarehouseID,c.Origin,c.id WarehouseName,c.Origin WarehouseCountry,d.logisticsservicemode SericeType,d.logisticsid Logicswayoid,d.fee Fee,d.fee OldFee,d.Fee_Box2,d.Fee_Box3 
,case when c.Origin ='{3}' then 1 else 0 end islocalWare
from (
select Warehouse, SUM(sl) kcsl,
isnull((select SUM(Qty) from scb_WMS_Pond where state=0 and Warehouse =a.Warehouse and ItemNo = '{0}'), 0) unshipped2,
ISNULL((select sum(s.sl) from scb_xsjl x inner join scb_xsjlsheet s on x.number=s.father
left join scbck c on x.warehouseid=c.number
left join scb_Logisticsmode d on x.logicswayoid=d.oid
where country='EU' and x.state=0 and filterflag>=7 and filterflag<=10 and not exists(select 1 from scb_WMS_Pond where state=0 and NID=x.number)
and isnull(ismark,'')=0 and x.temp3 not in ('4','7') and s.cpbh='{0}' and c.id=a.Warehouse),0) unshipped,
isnull((select SUM(qty)  from ods_wmseu_Stock_PreOccupationSheet where WarehouseId = Warehouse and ItemNumber = '{0}' and Status=0 and IsValid =1), 0) occupation
from scb_kcjl_location a inner join scb_kcjl_area_cangw b
on a.Warehouse = b.Location and a.Location = b.cangw
where b.state = 0 and a.Disable = 0  and a.LocationType<10 and b.cpbh = '{0}'
group by Warehouse) t inner join scbck c on
t.Warehouse=c.name inner join OSV_EU_LogisticsForProduct d 
on c.groupname=d.warehouse and IsChecked=1 and country='{3}' and d.goods='{0}'   --and signtouse=0 
and d.fee>0
where countryid='05'  and d.logisticsservicemode = 'DPD' and state=1  and c.IsMatching=1 and c.realname not like 'open%' and c.realname not like 'fb%' and kcsl-unshipped - unshipped2 -occupation >= {1}", CurrentItemNo, item.sl, datformat, model.temp10);
					var ignoreShipRuleList = db.Database.SqlQuery<OrderMatchEntity>(ignoreShipRuleSql).ToList();
					if (ignoreShipRuleList.Any(a => a.Origin == "PL"))
					{
						Checkeds.AddRange(ignoreShipRuleList);
					}
				}

				#endregion



				sw.Stop();
				//获取运行时间[毫秒]  
				Helper.Monitor(sw.ElapsedMilliseconds, model.number, $"源码行数：{st.GetFrame(0).GetFileLineNumber()} 执行查库存物流的sql");
				sw.Restart();

				Helper.Info(model.number, "wms_api", JsonConvert.SerializeObject(Checkeds));

				var tmp_Checkeds = new List<OrderMatchEntity>();

				//调整物流优先级
				Checkeds.ForEach(p =>
				{
					if (item.sl == 2)
					{
						//20250319 兰地花 指定两包发的货号
						var box2_cpbhs = "HW47195,CB10740SL,CB10770SL,CB10770BK,HW65976BK,HW65976WH,TL35306,HU10007".Split(',').ToList();
						if (box2_cpbhs.Any(o => o == item.cpbh))
						{
							p.isNeedSplit = false;
						}

						else if (p.Fee_Box2 > 0 && p.Fee_Box2 < p.Fee && p.WarehouseName == "EU05" && (p.Logicswayoid == 20 || p.Logicswayoid == 23))
						{
							p.Fee = (decimal)p.Fee_Box2;
							p.isNeedSplit = false;
						}
						else
						{
							p.isNeedSplit = true;
						}

					}
					else if (item.sl == 3)
					{
						if (p.Fee_Box3 > 0 && p.Fee_Box3 < p.Fee && p.WarehouseName == "EU05" && (p.Logicswayoid == 20 || p.Logicswayoid == 23))
						{
							p.Fee = (decimal)p.Fee_Box3;
							p.isNeedSplit = false;
						}
						else
						{
							p.isNeedSplit = true;
						}
					}

					//20250523 张洁楠 最优运费在意大利仓库的，且意大利仓库下有多个物流都是为该运费的，需要优先匹配DHL_IT
					if (p.WarehouseCountry == "IT" && p.SericeType == "DHL_IT")
					{
						p.logicsPriority = -1;
					}
					else
					{
						p.logicsPriority = 0;
					}

				});

				ws.Add(item.number, FilterByLogisticsV3(model, Checkeds, item, ref message));
				OrderMatch.Result.Message += message;

				sw.Stop();
				Helper.Monitor(sw.ElapsedMilliseconds, model.number, $"源码行数：{st.GetFrame(0).GetFileLineNumber()} FilterByLogistics");
			}

			if (ws.Where(p => p.Value.Count > 0).Any())
			{
				var Baselist = ws.FirstOrDefault().Value;
				if (ws.Count > 1)
				{
					foreach (var item in ws)
					{
						Baselist = Baselist.Where(f => item.Value.Any(g => g.WarehouseID == f.WarehouseID)).ToList();
					}
				}
				if (Baselist.Any())
				{
					MatchActualWarehouseEUV3(model.sheets, Baselist, ref OrderMatch);
				}
				else
					OrderMatch.Result.Message = "错误,N个不同的仓库，需要分拆订单";

				#region 注释
				//if (ws.Count() == 1)
				//{
				//	var CurrentWarehouse = ws.FirstOrDefault().Value.OrderBy(f => f.Fee).ThenByDescending(f => f.WarehouseID).FirstOrDefault();
				//	if (CurrentWarehouse == null)
				//	{
				//		OrderMatch.Result.Message = string.IsNullOrEmpty(OrderMatch.Result.Message) ? "匹配错误,库存不足" : OrderMatch.Result.Message;
				//	}
				//	else
				//	{
				//		OrderMatch.WarehouseID = CurrentWarehouse.WarehouseID;
				//		OrderMatch.WarehouseName = CurrentWarehouse.WarehouseName;
				//		OrderMatch.Logicswayoid = CurrentWarehouse.Logicswayoid;
				//		OrderMatch.SericeType = CurrentWarehouse.SericeType;
				//	}
				//}
				//else
				//{
				//	var Baselist = ws.FirstOrDefault().Value;
				//	foreach (var item in ws)
				//	{
				//		Baselist = Baselist.Where(f => item.Value.Any(g => g.WarehouseID == f.WarehouseID)).ToList();
				//	}
				//	if (Baselist.Any())
				//	{
				//		var CurrentWarehouse = Baselist.OrderBy(f => f.Fee).ThenByDescending(f => f.WarehouseID).FirstOrDefault();
				//		OrderMatch.WarehouseID = CurrentWarehouse.WarehouseID;
				//		OrderMatch.WarehouseName = CurrentWarehouse.WarehouseName;
				//		OrderMatch.Logicswayoid = CurrentWarehouse.Logicswayoid;
				//		OrderMatch.SericeType = CurrentWarehouse.SericeType;
				//	}
				//	else
				//		OrderMatch.Result.Message = "错误,N个不同的仓库，需要分拆订单";
				//}
				#endregion
			}
			else
			{
				OrderMatch.Result.Message = string.IsNullOrEmpty(OrderMatch.Result.Message) ? "匹配错误,库存不足" : OrderMatch.Result.Message;
			}

			#endregion

		}

		private void MatchWarehouseByGB_V3(scb_xsjl Order, ref OrderMatchResult OrderMatch)
		{
			#region 初始化

			#region 黑名单 20251105 史倩文
			var blacklist = new List<string>() { "MIDDLETON CLOSE CHINGFORD", "34 Crowhill Avenue", "31 Maxwell Close", "251 rye lane peckham rye" };
			string addr1 = (Order.address1 ?? "").ToLower();
			string addr2 = (Order.adress ?? "").ToLower();
			if (blacklist.Any(o => o.ToLower() == addr1 || o.ToLower() == addr2))
			{
				OrderMatch.Result.Message = "此单疑似黑名单，请取消！";
				return;
			}

			#endregion

			var db = new cxtradeModel();
			var Tocountry = Order.temp10; //收货国家
			var datformat = DateTime.Now.ToString("yyyy-MM-dd");
			var sheets = db.scb_xsjlsheet.Where(f => f.father == Order.number).ToList();
			var IsMultiPackage = sheets.Count > 1 || sheets.Any(f => f.sl > 1);
			var NoMulti = false;
			bool canAB = true;
			#endregion

			#region 多包判断
			double weight = 0;//实重
			double weight_vol = 0;//体积重
			double DXweight_vol = 0;//DX体积重 除以5000
			double Mins = 0;//最小边
			double Size = 0;//尺寸
			double Change = 0;
			double Volume = 0;
			bool IsDHLExceed = false; //DHL超标件
			bool IsDXExceed = false; //DX超标件
			double weight_sum = 0; //实重、体积重取最大值
			double billWeight = 0; //计费重
			var matchSize = new List<double>();
			var realSize = new List<double>();

			var dat2 = DateTime.Now;
			var dw = dat2.DayOfWeek.ToString("d");

			//if (sheets.Any(p => p.cpbh.ToUpper() == "CB10442DK"))
			//{
			//	OrderMatch.Result.Message = "货号：CB10442DK，超重，建议改发DX ";
			//	return;
			//}
			#region 节假日后第一天不合并发货
			if (DateTime.Now > new DateTime(2023, 6, 10, 0, 0, 0) && IsMultiPackage && (dw == "1" || dw == "0" || dw == "6"))
			{
				OrderMatch.Result.Message = "不允许多包，请拆分后重新申请";
				NoMulti = true;
				canAB = false;
				//return;
			}
			//20250430 王晓兰 然后5.24-5.27也不能打包发货 假期后的第一天不多包发
			if (dat2 >= new DateTime(2025, 5, 24, 0, 0, 0) && dat2 < new DateTime(2025, 5, 28, 0, 0, 0) && IsMultiPackage)
			{
				OrderMatch.Result.Message = "不允许多包，请拆分后重新申请";
				NoMulti = true;
				canAB = false;
				//return;
			}
			#endregion

			//2022-06-07 龚奇
			//A/B/C 这种完全不同的产品，即使尺寸符合标准件，也不建议合并发
			var ss = sheets.Select(o => o.cpbh).Distinct();
			if (ss.Count() > 1)
			{
				OrderMatch.Result.Message = "多包订单存在不同cpbh，请拆分后重新申请";
				NoMulti = true;
				return;
			}
			#endregion

			#region 仓库匹配

			var ws = new Dictionary<string, List<OrderMatchEntityGB>>();
			var sheetsgroup = sheets.Select(a => a.cpbh).Distinct().ToList();
			foreach (var item in sheetsgroup)
			{
				var qty = sheets.Where(a => a.cpbh == item).ToList().Sum(a => a.sl);
				var Sql = string.Format(@" select Warehouse WarehouseName,s.number WarehouseID,s.AreaSort,t.Kcsl,unshipped,lockqty, CASE t.Warehouse WHEN 'GB98' THEN '1999-01-01' ELSE r.jcrq end as jcrq from (
select Warehouse, SUM(sl) kcsl,SUM(isnull(lockqty,0)) lockqty,
isnull((select SUM(sl) tempkcsl from scb_tempkcsl where nowdate = '{2}' and ck = Warehouse and cpbh = '{0}'), 0) tempkcsl,
isnull((select SUM(qty)  from tmp_pickinglist_occupy where cangku = Warehouse and cpbh = '{0}'), 0) unshipped
from scb_kcjl_location a inner join scb_kcjl_area_cangw b
on a.Warehouse = b.Location and a.Location = b.cangw
where b.state = 0 and a.Disable = 0  and b.cpbh = '{0}' and b.location in ('GB06','GB07','GB98') AND a.LocationType<10
group by Warehouse) t 
INNER JOIN dbo.scbck s ON s.id=t.Warehouse  
LEFT JOIN scb_kcjl_area r ON r.cpbh='{0}' AND r.place=t.Warehouse
where kcsl - tempkcsl-unshipped-lockqty  >= {1}", item, qty, datformat);
				var Warehouse = db.Database.SqlQuery<OrderMatchEntityGB>(Sql).ToList();
				Helper.Info(Order.number, "wms_api", JsonConvert.SerializeObject(Warehouse));

				var dat = AMESTime.AMESNowGB;
				if (dat.Hour >= 9 && dat.Hour < 17)
				{
					//当地时间9点后不分配restock仓库
					Warehouse = Warehouse.Where(o => o.WarehouseName != "GB98").ToList();
				}
				//Gymaxmore 不发restock
				//2024 王鑫月 Gymaxmore 可以发restock，HOMFME不发restock
				//2024 0823王晓兰HOMFME 亚马逊这家店之前设置过不发restock，取消吧，以后和其他店铺一样优先发
				//if (Order.dianpu.ToUpper() == "HOMFME")
				//{
				//	Warehouse = Warehouse.Where(o => o.WarehouseName != "GB98").ToList();
				//}
				var carriers_warehouse = db.Database.SqlQuery<scb_order_carrier>(string.Format("select * from scb_order_carrier where orderid='{0}' and dianpu='{1}' and oid is not null and temp>0 "
					, Order.OrderID, Order.dianpu)).ToList();
				if (carriers_warehouse.Any())
				{
					var order_carrier = carriers_warehouse.First();
					if (Warehouse.Any(a => a.WarehouseName.Contains(order_carrier.temp2)))
					{
						Warehouse = Warehouse.Where(a => a.WarehouseName == order_carrier.temp2).ToList();
					}
				}

				if (Warehouse.Any())
				{
					ws.Add(item, Warehouse);
				}
				else
				{
					ws.Add(item, null);
				}
			}
			if (!ws.Any(f => f.Value == null))
			{
				//校验是否禁发restock
				//20251117 朱军德 英国：FDSUK-Marketing 不发 restock 的
				var canSendRestock = true;
				var b2cdianpu = new List<string> { "costway-robertdyas", "costway-wilko", "dropshop-uk", "fdsuk", "fdsuk-bp", "fdsuk-marketing" };
				if (Order.dianpu.ToUpper().StartsWith("FDS") || b2cdianpu.Contains(Order.dianpu.ToLower()))
				{
					canSendRestock = false;
				}

				//匹配仓库
				if (ws.Count() == 1) //只有一种商品
				{
					if (ws.FirstOrDefault().Value.Exists(p => p.WarehouseName == "GB98") && canSendRestock)//!Order.dianpu.ToUpper().StartsWith("FDS") //若匹配到含restock仓，就优先发此仓; 2022-07-13 FDS开头的店铺不进行restock分配
					{
						OrderMatch.WarehouseID = 88;
						OrderMatch.WarehouseName = "GB98";
					}
					else //没有restock仓
					{
						#region 注释
						//在scbck 设置AreaSort优先级，U4>U3
						//var CurrentWarehouse = ws.FirstOrDefault().Value.Where(p => p.WarehouseName != "GB98").OrderBy(f => f.AreaSort).ThenByDescending(f => f.Kcsl).FirstOrDefault();
						#endregion 注释
						//按进仓顺序,优先发先进仓的仓库
						var CurrentWarehouse = ws.FirstOrDefault().Value.Where(p => p.WarehouseName != "GB98").OrderBy(f => f.jcrq).ThenByDescending(f => f.Kcsl).FirstOrDefault();

						#region 20240221 梅雪敏 优先推送订单给U3仓，从2.21到3.10, 每周一保持原样不做修改，周二至周日做调整
						var dat = AMESTime.AMESNowGB;
						if (dat >= new DateTime(dat.Year, 2, 21, 0, 0, 0, 0) && dat < new DateTime(dat.Year, 3, 11, 0, 0, 0, 0) && dat.DayOfWeek != DayOfWeek.Monday)
						{
							CurrentWarehouse = ws.FirstOrDefault().Value.Where(p => p.WarehouseName != "GB98").OrderByDescending(f => f.AreaSort).ThenByDescending(f => f.Kcsl).FirstOrDefault();
						}
						#endregion

						if (CurrentWarehouse == null)
						{
							OrderMatch.Result.Message = "匹配错误,库存不足";
							return;
						}
						else
						{
							OrderMatch.WarehouseID = CurrentWarehouse.WarehouseID;
							OrderMatch.WarehouseName = CurrentWarehouse.WarehouseName;
						}
					}
				}
				else //有多种商品
				{
					var Baselist = ws.FirstOrDefault().Value;
					foreach (var item in ws)
					{
						Baselist = Baselist.Where(f => item.Value.Any(g => g.WarehouseID == f.WarehouseID)).ToList();
					}
					if (Baselist.Any()) //能匹配到仓库
					{
						if (Baselist.Exists(p => p.WarehouseName == "GB98") && canSendRestock) //若匹配到含restock仓，就优先发此仓; 2022-07-13 FDS开头的店铺不进行restock分配
						{
							OrderMatch.WarehouseID = 88;
							OrderMatch.WarehouseName = "GB98";
						}
						else
						{
							//var CurrentWarehouse = Baselist.Where(p => p.WarehouseName != "GB98").OrderBy(f => f.AreaSort).ThenByDescending(f => f.Kcsl).FirstOrDefault();
							//按进仓顺序,优先发先进仓的仓库
							var CurrentWarehouse = Baselist.Where(p => p.WarehouseName != "GB98").OrderBy(f => f.jcrq).ThenByDescending(f => f.Kcsl).FirstOrDefault();

							#region 20240221 梅雪敏 优先推送订单给U3仓，从2.21到3.10, 每周一保持原样不做修改，周二至周日做调整
							var dat = AMESTime.AMESNowGB;
							if (dat >= new DateTime(dat.Year, 2, 21, 0, 0, 0, 0) && dat < new DateTime(dat.Year, 3, 11, 0, 0, 0, 0) && dat.DayOfWeek != DayOfWeek.Monday)
							{
								CurrentWarehouse = Baselist.Where(p => p.WarehouseName != "GB98").OrderByDescending(f => f.AreaSort).ThenByDescending(f => f.Kcsl).FirstOrDefault();
							}
							#endregion


							if (CurrentWarehouse == null)
							{
								OrderMatch.Result.Message = "匹配错误,库存不足";
								return;
							}
							else
							{
								OrderMatch.WarehouseID = CurrentWarehouse.WarehouseID;
								OrderMatch.WarehouseName = CurrentWarehouse.WarehouseName;
							}
						}
					}
					else
					{
						OrderMatch.Result.Message = "错误,N个不同的仓库，需要分拆订单";
						return;
					}
				}

			}
			else
			{
				OrderMatch.Result.Message = "匹配错误,库存不足";
				return;
			}

			#endregion

			#region 指定物流
			bool IsFind = true;
			if (IsFind)
			{
				var order_carriers = db.Database.SqlQuery<scb_order_carrier>(string.Format("select * from scb_order_carrier where orderid='{0}' and dianpu='{1}' and oid is not null and oid>0 "
					, Order.OrderID, Order.dianpu)).ToList();
				if (order_carriers.Any())
				{
					var order_carrier = order_carriers.First();
					OrderMatch.Logicswayoid = (int)order_carrier.oid;
					OrderMatch.SericeType = order_carrier.ShipService;
					//暂时还是指定GB06仓库
					//OrderMatch.WarehouseID = 83;
					//OrderMatch.WarehouseName = "GB06";
					return;
				}
			}

			//XDP 
			// 2023-02-14 XDP先去掉，这个物流我们暂时先停用，这些全部转给DHL
			//2023-02-24 恢复xdp
			var XDPGoods = db.Database.SqlQuery<string>("select ItemNO from scb_WMS_MatchRule where type='Only_XDP'").ToList().FirstOrDefault();
			if (sheets.Any(f => XDPGoods.Contains(f.cpbh + ",")))
			{
				OrderMatch.Logicswayoid = 29;
				OrderMatch.SericeType = "ECON";
				//暂时还是指定GB06仓库
				//OrderMatch.WarehouseID = 83;
				//OrderMatch.WarehouseName = "GB06";
				return;
			}
			#endregion

			#region 物流匹配
			//var scb_LogisticsForWeight = db.scb_LogisticsForWeight.Where(f => f.Warehouse == "IPSWICH" && f.IsChecked == 1).OrderBy(f => f.Sort);
			bool isDX_2Man = true;
			bool isDX_Freight = true;
			bool isDHL = true;
			bool isDPD = true;


			#region 黑名单
			// 20231025 武金凤 以下地址建议取消DX
			var blackAdressList = new List<string>()
					{
						"Northern Ireland",
						"Dublin",
						"Rest of Eire",
						"Channel Islands",
						"Isle Of Man",
						"Grampian",
						"Highlands",
						"Scottish Offshore",
						"Isle of Wight"
					};
			if (blackAdressList.Any(p => p.ToLower() == addr1 || p.ToLower() == addr2))
			{
				isDX_2Man = false;
				isDX_Freight = false;
				Helper.Info(Order.number, "wms_api", $"adress:{Order.adress}; address1:{Order.address1} 此单疑似DX地址黑名单");
				OrderMatch.Result.Message = "此单疑似DX地址黑名单,建议取消!";
				//return;
			}

			if (BlackAddress(Order.dianpu, Order.xspt, Order.khname, Order.adress, Order.address1, Order.city, Order.country, Order.email, Order.fkzh, Order.phone, Order.bz))
			{
				OrderMatch.Result.Message = $"退单率高，Hold单，请确认是否发货！";
				return;
			}
			#endregion

			var isDXExceed = false;
			var goodInfos = new List<GoodsModel>();


			foreach (var sheet in sheetsgroup)
			{
				var qty = sheets.Where(a => a.cpbh == sheet).ToList().Sum(a => a.sl);
				var sizes = new List<double?>();
				var good = db.N_goods.Where(g => g.cpbh == sheet && g.country == "GB" && g.groupflag == 0).FirstOrDefault();
				sizes.Add(good.bzgd);
				sizes.Add(good.bzcd);
				sizes.Add(good.bzkd);
				var weight_pre = (double)good.weight * qty / 1000;//计算实重
				weight += weight_pre;//(double)good.weight * sheet.sl / 1000; //计算实重累计
				var weight_vol_pre = (double)(good.bzcd * good.bzgd * good.bzkd * qty / 6000); //计算体积重
				weight_vol += weight_vol_pre;//(double)(good.bzcd * good.bzgd * good.bzkd * sheet.sl / 6000); //计算体积重
				var DXweight_vol_pre = (double)(good.bzcd * good.bzgd * good.bzkd * qty / 5000); //计算DX体积重
				DXweight_vol += DXweight_vol_pre;//(double)(good.bzcd * good.bzgd * good.bzkd * sheet.sl / 5000); //计算DX体积重
				Volume += (double)(good.bzcd * good.bzgd * good.bzkd * qty / 1000000);  //体积

				var height = (double)sizes.Min() * qty;
				matchSize = new List<double>() { height, (double)good.bzcd, (double)good.bzkd };

				goodInfos.Add(new GoodsModel()
				{
					cpbh = sheet,
					qty = qty,
					bzcd = (decimal)good.bzcd,
					bzkd = (decimal)good.bzkd,
					bzgd = (decimal)good.bzgd,
					weight_g = (decimal)good.weight,
				});


				#region 真实尺寸
				var realSizeItem = db.scb_realsize.Where(f => f.cpbh == sheet && f.country == "GB").FirstOrDefault();
				realSize = matchSize;
				if (realSize != null)
				{
					var height_real = (double)realSizeItem.bzgd * qty;
					realSize = new List<double>() { height, (double)realSizeItem.bzcd, (double)realSizeItem.bzkd };
				}
				#endregion


				if ((sizes.Max() > 300 || good.weight / 1000 > 50 || DXweight_vol_pre / qty > 100))
				{
					isDXExceed = true;
				}
				//单个 小于80cm, 小于10kg，打包后 不超过30kg，不超过80cm  可以合并发货，反之不能
				if (IsMultiPackage && ((sizes.Max() >= 80 || (good.weight / 1000) >= 10) || weight_pre > 30 || matchSize.Max() > 80))
				{
					canAB = false;
				}
			}
			//20250321 王晓兰 临界值的尺寸调整成正常，匹配全部按照实际重量来
			//weight = weight == 30.2 ? 30 : weight; //2023-02-24 DHL重量边界值调整

			billWeight = Math.Ceiling(weight_vol > weight ? weight_vol : weight); //计费重
			matchSize = matchSize.OrderByDescending(p => p).ToList();
			realSize = realSize.OrderByDescending(p => p).ToList();


			//判断是否符合DX 2man 符合 return
			//20250121 王晓兰 太大件送货员都搬不动，不用考虑合并发货是否达到dx了，就和正常多个的一样去看DHL和DX_freigh，DX只发单个的
			if (matchSize[0] > 300 || DXweight_vol > 100 || weight > 50)
			{
				//单包，且符合DX，直接DX
				if (!IsMultiPackage)
				{
					if (isDX_2Man)
					{
						OrderMatch.Logicswayoid = 103;
						OrderMatch.SericeType = "DX";
						Helper.Info(Order.number, "wms_api", $"体积重{DXweight_vol}，毛重{weight}，发DX 2 man");
						return;
					}
					else
					{
						OrderMatch.SericeType = "";
						OrderMatch.Result.Message = $"无可用物流。{OrderMatch.Result.Message}";
						return;
					}
				}
				else  //多包
				{
					//如果存在超DX尺寸的，提示拆单，发DX
					if (isDXExceed)
					{
						if (!isDX_2Man)
						{
							OrderMatch.SericeType = "";
							OrderMatch.Result.Message = $"无可用物流。{OrderMatch.Result.Message}";
							return;
						}
						else
						{
							OrderMatch.SericeType = "";
							OrderMatch.Result.Message = $"建议拆单,可发DX。";
							return;
						}
					}
				}
			}


			//剩下的标准件和轻度超标件开始比价了

			#region isDPD
			var dpd_dat = AMESTime.AMESNowGB;
			var dayOfWeek = dpd_dat.DayOfWeek;
			//旺季匹配设置
			var StartDate = new DateTime(2026, 1, 2); // 2026-1-2
			var EndDate = new DateTime(2025, 11, 17); // 2025-11-17
			if (DateTime.Now > EndDate && DateTime.Now < StartDate)
			{
				var OrderLimit = db.Database.SqlQuery<scb_DPD_OrderLimit>(string.Format("select * from [scb_DPD_OrderLimit] where date='{0}' "
					, DateTime.Now.ToString("yyyy-MM-dd"))).ToList();
				if (OrderLimit.Any())
				{
					DateTime today = DateTime.Today;
					if (sheets.Any(p => p.cpbh.StartsWith("TA10038") || p.cpbh.StartsWith("NP12481BK") || p.cpbh.StartsWith("SP37604") || p.cpbh.StartsWith("SP35778") || p.cpbh.StartsWith("TL35688")) && (today.DayOfWeek == DayOfWeek.Saturday || today.DayOfWeek == DayOfWeek.Sunday))
					{
						isDPD = true;
						Helper.Info(Order.number, "wms_api", $"当前为{dpd_dat}，{dayOfWeek}，TA10038 SP37604 SP35778 TL35688这几个系列的货号改成周六周日也参与DPD匹配。");
					}
					else if (isDPD && weight < 30 && matchSize[0] <= 122 && matchSize[1] <= 60 && matchSize[2] <= 60)
					{
						if ((IsMultiPackage && canAB) || !IsMultiPackage)
						{
							var warehouse = OrderMatch.WarehouseName;
							var warehouseid = OrderMatch.WarehouseID;
							var orderlimit = OrderLimit.Sum(a => a.Limit);
							if (orderlimit != null)
							{
								var dpdCount = db.scb_xsjl.Where(p => p.country == "GB" && p.logicswayoid == 19 && p.state == 0 && p.filterflag < 11).Count();
								if (dpdCount < orderlimit)
								{

								}
								else
								{
									isDPD = false; //DPD发不了，单量限制超了
									Helper.Info(Order.number, "wms_api", $"DPD {dayOfWeek}打单{orderlimit}件限制");
								}
							}
						}
					}
					else
					{
						isDPD = false; //DPD发不了，尺寸超标，或仓库不支持
					}
				}
			}
			else
			{
				//20250915 莫有缘 DPD0915停掉
				//20250916 莫有缘 周六周日直接跳出，不走 DPD 判断逻辑
				if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday || ((dayOfWeek == DayOfWeek.Friday) && dpd_dat.Hour >= 14))
				{
					isDPD = false;
					Helper.Info(Order.number, "wms_api", $"当前为{dpd_dat}，{dayOfWeek}，周末不执行DPD自动匹配逻辑周一到周五正常匹配。");
				}
				//20250811 莫有缘 DPD 用真实尺寸，只要符合这个条件100cm≤长≤120cm,宽≤60cm,高≤60cm,毛重30kg内就直接发DPD,不用跟DHL进行对比
				//20250908 莫有缘 试用期间 3方DPD  先发80cm≤长≤120cm,宽≤60cm,高≤60cm,毛重30kg内，一口价3.8GBP, 只发GB07，2025-9-9才有车，8号截单后才开匹配
				//20251014 莫有缘  TA10038 SP37604	SP35778 TL35688 然后这几个系列的货号改成周六周日也参与DPD匹配
				//20251016 王晓兰 DPD 长121 122都改成120去发
				if (OrderMatch.WarehouseName == "GB07" && sheets.Any(p => p.cpbh.StartsWith("TA10038") || p.cpbh.StartsWith("NP12481BK") || p.cpbh.StartsWith("SP37604") || p.cpbh.StartsWith("SP35778") || p.cpbh.StartsWith("TL35688")))
				{
					isDPD = true;
					Helper.Info(Order.number, "wms_api", $"当前为{dpd_dat}，{dayOfWeek}，TA10038 SP37604 SP35778 TL35688这几个系列的货号改成周六周日也参与DPD匹配。");
				}
				//20251017 莫有缘 改成只要小于122的都能发
				else if (isDPD && OrderMatch.WarehouseName == "GB07" && weight < 30 && matchSize[0] <= 122 && matchSize[1] <= 60 && matchSize[2] <= 60)
				{
					if ((IsMultiPackage && canAB) || !IsMultiPackage)
					{
						var dat_start = new DateTime(dpd_dat.Year, dpd_dat.Month, dpd_dat.Day, 0, 0, 0, 0);
						var dat_end = dat_start.AddDays(1);
						var dpdCount = db.scb_xsjl.Where(p => p.country == "GB" && p.logicswayoid == 19 && p.state == 0 && p.filterflag < 11
						).Count();

						if (dpdCount < 800)
						{
							//OrderMatch.Logicswayoid = 19;
							//OrderMatch.SericeType = "DPD";
							////OrderMatch.MatchFee = 3.8m;
							//Helper.Info(Order.number, "wms_api", $"80cm≤长≤120cm,宽≤60cm,高≤60cm,毛重30kg内就直接发DPD,不用跟DHL进行对比,只发GB07");
							//return;
						}
						else
						{
							isDPD = false; //DPD发不了，单量限制超了
							Helper.Info(Order.number, "wms_api", $"DPD:{dpdCount} 周一到周五打单800件的限制");
						}
					}
				}
				else
				{
					isDPD = false; //DPD发不了，尺寸超标，或仓库不支持
				}
			}

			//20251022 莫有缘 TikTok 不发DPD
			if (Order.xspt.ToLower() == "tiktok" && isDPD)
			{
				isDPD = false;
			}
			#endregion

			//20251008 莫有缘  DHL发往这些邮编的 超过30千克的禁发
			if (billWeight > 30 && isDHL)
			{
				var zipTmp = Order.zip.Replace(" ", "");
				var searchZip = zipTmp.Substring(0, zipTmp.Length - 3);
				var banZips = "BT1,BT10,BT11,BT12,BT13,BT14,BT15,BT16,BT17,BT18,BT19,BT2,BT20,BT21,BT22,BT23,BT24,BT25,BT26,BT27,BT28,BT29,BT3,BT30,BT31,BT32,BT33,BT34,BT35,BT36,BT37,BT38,BT39,BT4,BT40,BT41,BT42,BT43,BT44,BT45,BT46,BT47,BT48,BT49,BT5,BT51,BT52,BT53,BT54,BT55,BT56,BT57,BT58,BT6,BT60,BT61,BT62,BT63,BT64,BT65,BT66,BT67,BT68,BT69,BT7,BT70,BT71,BT74,BT75,BT76,BT77,BT78,BT79,BT8,BT80,BT81,BT82,BT9,BT92,BT93,BT94"
					.Split(',').ToList();

				isDHL = !banZips.Any(p => p.ToLower() == searchZip.ToLower());   // false;
				Helper.Info(Order.number, "wms_api", $"计费重 {billWeight}>30,邮编:{searchZip} 禁发DHL");
			}

			//后面会获取运费，要比价
			////DHL标准件
			//if (isDHL && billWeight < 30 && matchSize[0] <= 120 && matchSize[1] <= 80 && matchSize[2] <= 80)
			//{
			//	if ((IsMultiPackage && canAB) || !IsMultiPackage)
			//	{
			//		OrderMatch.Logicswayoid = 20;
			//		OrderMatch.SericeType = "NEXTDAY";
			//		Helper.Info(Order.number, "wms_api", $"属于DHL标准件，还是发DHL");
			//		return;
			//	}
			//}


			//DHL超标件
			var matchlogicList = new List<MatchFeeResultModel>();
			if (isDPD)
			{
				var freightCalResult_tmp = FreightCalculation_GBV2(goodInfos, Order.zip, Order.country, 19, canAB, IsMultiPackage);
				matchlogicList.Add(freightCalResult_tmp);
			}
			if (isDHL)
			{
				var freightCalResult_DHL = FreightCalculation_GBV2(goodInfos, Order.zip, Order.country, 20, canAB, IsMultiPackage);
				matchlogicList.Add(freightCalResult_DHL);
			}
			if (isDX_Freight)
			{
				var freightCalResult_DX = FreightCalculation_GBV2(goodInfos, Order.zip, Order.country, 131, canAB, IsMultiPackage);
				matchlogicList.Add(freightCalResult_DX);
			}
			Helper.Info(Order.number, "wms_api", $"匹配运费：{JsonConvert.SerializeObject(matchlogicList)}");

			var matchmessage = $"canAB:{(canAB ? "true" : "false")} ";
			foreach (var item in matchlogicList)
			{
				matchmessage += $" {(item.logicswayoid == 20 ? "DHL" : item.serviceType)}:{(item.state ? item.matchfee.ToString() : item.message)}；";
			}

			matchlogicList = matchlogicList.Where(p => p.state).OrderBy(p => p.matchfee).ToList();
			if (matchlogicList.Any())
			{
				var matchlogic = matchlogicList.First();
				if (matchlogic.logicswayoid != 131 && IsMultiPackage && !canAB)
				{
					OrderMatch.Result.Message = $"合并发货尺寸超限制，请拆单。{matchmessage}";
					OrderMatch.SericeType = "";
				}
				else
				{
					if (matchlogic.serviceType == "DX_Freight_Split" && IsMultiPackage)
					{
						OrderMatch.Result.Message = "建议拆单,可发DX_Freight_Split。" + matchmessage;
						OrderMatch.SericeType = "";
						return;
					}
					OrderMatch.Result.Message = matchmessage;
					OrderMatch.Logicswayoid = matchlogic.logicswayoid;
					OrderMatch.SericeType = matchlogic.serviceType;
					OrderMatch.MatchFee = matchlogic.matchfee;
				}
			}

			#region 注释
			//var freightCalResult_DHL = FreightCalculation_GB(FreightCalModel, Order.zip, Order.country, 20);
			//var freight_DHLExceed = freightCalResult_DHL.Item1;
			//Helper.Info(Order.number, "wms_api", $"DHL超标件运费：{freightCalResult_DHL.Item2}");
			////20250122 龚奇 dx_freight 不合并发货了
			//var isDXFreightMerge = false;
			//var freightCalResult_DX = FreightCalculation_GB(FreightCalModel, Order.zip, Order.country, 131, isDXFreightMerge);
			//var freight_DXExceed = freightCalResult_DX.Item1;
			//Helper.Info(Order.number, "wms_api", $"DX Freight超标件运费：{freightCalResult_DX.Item2}");

			//OrderMatch.Result.Message = $@"DHL超标件。DHL:{freight_DHLExceed.ToString("F2")} {(freight_DHLExceed > 0 ? "" : freightCalResult_DHL.Item2)};{(isDXFreightMerge ? "DX_Freight_Merge" : "DX_Freight_Split")}:{freight_DXExceed.ToString("F2")} {(freight_DXExceed > 0 ? "" : freightCalResult_DX.Item2)};";

			//if (freight_DHLExceed < freight_DXExceed || !isDX_Freight)
			//{
			//	OrderMatch.Logicswayoid = 20;
			//	OrderMatch.SericeType = "NEXTDAY";
			//	return;
			//}
			//else
			//{
			//	OrderMatch.Logicswayoid = 131;
			//	//OrderMatch.SericeType = "DX_Freight_Merge";
			//	OrderMatch.SericeType = "DX_Freight_Split";
			//	return;
			//}
			#endregion

			////20231023 王晓兰 CB10442DK 这个做个限制吧，提示超重，改发DX
			//if (sheets.Any(p => p.cpbh.ToUpper() == "CB10442DK"))
			//{
			//	OrderMatch.Result.Message += "货号：CB10442DK，超重，建议改发DX ";
			//}
			#endregion
		}

		private string GetGBusfulZip(string zip)
		{
			var zipTmp = Regex.Replace(zip, @"[^a-zA-Z0-9]", "");
			if (string.IsNullOrEmpty(zipTmp) || zipTmp.Length < 3)
				throw new Exception($"zip:{zip}无效，请确认！");

			var searchZip = zipTmp.Substring(0, zipTmp.Length - 3);
			return searchZip;
		}

		public MatchFeeResultModel FreightCalculation_GBV2(List<GoodsModel> goods, string zip, string country, int logicswayoid, bool canAB, bool IsMultiPackage)
		{
			var result = new MatchFeeResultModel() { logicswayoid = logicswayoid, state = false };
			try
			{
				//DHL
				if (logicswayoid == 20)
				{
					var db = new cxtradeModel();
					var sql = $" select  cxtrade.dbo.gb_getLogistics_zone_new2('{zip}','{country}',{logicswayoid})";
					//后面会改成int，先手动过渡下
					var zone = int.Parse(db.Database.SqlQuery<string>(sql).FirstOrDefault());
					//var zonetmp = db.Database.SqlQuery<string>(sql).FirstOrDefault();
					//var zone = 0;
					//if (zonetmp == "GB.ZoneA")
					//	zone = 1;
					//else if (zonetmp == "GB.ZoneB")
					//	zone = 2;
					//else if (zonetmp == "GB.ZoneC")
					//	zone = 3;
					//else if (zonetmp == "GB.ZoneD")
					//	zone = 4;

					//baseCharge = 基本运费, surcharge = 附加费;
					decimal baseCharge = 0, surcharge = 0;
					//p1 = 0, p2 = 0, p3 = 超尺寸附加费, p4 = 燃油附加费;
					decimal p1 = 0, p2 = 0, p3 = 0, p4 = 1.095m + 0.028m;

					string formula = string.Empty;
					decimal fee = 0;
					List<string> chargeItems = new List<string>();
					string item = "";

					switch (zone)
					{
						case 1:
						case 2:
							p1 = 4.79m;
							p2 = 0.32m;
							break;
						case 3:
							p1 = 10.82m;
							p2 = 0.42m;
							break;
						case 4:
							p1 = 14.42m;
							p2 = 0.53m;
							break;
						case 5:
							p1 = 10.78m;
							p2 = 0.32m;
							break;
						default:
							throw new Exception($"邮编：{zip} 在未知区域，请确认是否正确！");
							break;
					}

					foreach (var good in goods)
					{
						var length = 0m;
						var width = 0m;
						var height = 0m;
						var weight_vol = 0m;
						var weight_kg = 0m;
						if (canAB)
						{
							var mergeHigh = good.bzgd * good.qty;
							var size = new List<decimal>() { mergeHigh, good.bzcd, good.bzkd };
							size = size.OrderByDescending(p => p).ToList();
							length = size[0];
							width = size[1];
							height = size[2];
							weight_kg = Math.Ceiling(good.weight_g * good.qty / 1000 * 10) / 10m;
							weight_vol = good.bzcd * good.bzkd * good.bzgd * good.qty / 6000;//计算体积重
						}
						else
						{
							length = good.bzcd;
							width = good.bzkd;
							height = good.bzgd;
							weight_kg = Math.Ceiling(good.weight_g / 1000 * 10) / 10m;
							weight_vol = good.bzcd * good.bzkd * good.bzgd / 6000;//计算体积重
						}

						//20250321 王晓兰 临界值的尺寸调整成正常，匹配全部按照实际重量来
						//weight_kg = weight_kg == 30.2m ? 30 : weight_kg; //2023-02-24 DHL重量边界值调整
						//20251016 王晓兰  长121 122都改成120去发
						length = length == 121 || length == 122 ? 120 : length;

						var billWeight = Math.Ceiling(weight_vol > weight_kg ? weight_vol : weight_kg); //计费重


						if (length <= 120 && width <= 80 && height <= 80) //标准件  2025.1.20
						{
							if (billWeight <= 30m)
							{
								switch (zone)
								{
									case 1:
									case 2:
										baseCharge = 5.38m;
										break;
									case 3:
										baseCharge = 12.15m;
										break;
									case 4:
										baseCharge = 16.19m;
										break;
									case 5:
										baseCharge = 12.11m;
										break;
									default:
										throw new Exception($"邮编：{zip} 在未知区域，请确认是否正确！");
										break;
								}
								//chargeItems.Add($"基本运费={baseCharge}");
								formula = $"{baseCharge}";
							}
							else
							{
								surcharge = 12.5m;
								baseCharge = ((p1 + (billWeight - 30) * p2) * p4) + surcharge;
								formula = $"(({p1}+({billWeight}-30)*{p2})*{p4})+{surcharge}";
								chargeItems.Add($"计算重超30，附加费={surcharge}");
							}
							chargeItems.Add($"运费={formula}={baseCharge}");
						}
						// 超标件
						else
						{
							if (billWeight <= 30m)
							{
								switch (zone)
								{
									case 1:
									case 2:
										baseCharge = 10.99m;
										break;
									case 3:
										baseCharge = 17.77m;
										break;
									case 4:
										baseCharge = 21.81m;
										break;
									case 5:
										baseCharge = 17.72m;
										break;
									default:
										throw new Exception($"邮编：{zip} 在未知区域，请确认是否正确！");
										break;
								}
								formula = $"{baseCharge}";
							}
							else
							{
								p3 = 5m;
								surcharge = 12.5m;
								baseCharge = ((p1 + (billWeight - 30) * p2 + p3) * p4);
								baseCharge += surcharge;
								formula = $"(({p1}+({billWeight}-30)*{p2}+{p3})*{p4})+{surcharge}";
								chargeItems.Add($"计算重超30，附加费={surcharge}");
							}

							if ((length > 120 && length < 200) || good.bzkd > 80)
							{
								surcharge = 3.5m;
								baseCharge += surcharge;
								formula = $"({formula})+{surcharge}";
								chargeItems.Add($"120<单边<200，附加费={surcharge}");
							}
							else if (length >= 200)
							{
								surcharge = 30m;
								baseCharge += surcharge;
								formula = $"({formula})+{surcharge}";
								chargeItems.Add($"单边>=200，附加费={surcharge}");
							}
							chargeItems.Add($"基本运费={formula}={baseCharge}");
						}

						//不能合并发的，单个包裹运费累计求和
						if (!canAB)
						{
							baseCharge = baseCharge * good.qty;
							formula = $"({formula})*{good.qty}";
						}
						fee += baseCharge;
						formula = $"{formula}+";
					}
					result.matchfee = fee;
					result.message = formula.Substring(0, formula.Length - 1) + "=" + fee;
					result.serviceType = "NEXTDAY";
					result.state = true;
				}
				else if (logicswayoid == 131)
				{
					var db = new cxtradeModel();
					//var baseCharge = 9.912m;
					var searchZip = GetGBusfulZip(zip);

					if (string.IsNullOrEmpty(searchZip))
					{
						throw new Exception($"zip:{zip} 获取DX_Freight基础运费失败，邮编无效");
					}
					var DX_F_Surcharge = db.GB_Logistics_Surcharge.Where(p => p.LogisticsId == logicswayoid && (p.Zip == searchZip || string.IsNullOrEmpty(p.Zip))).OrderByDescending(p => p.Zone).FirstOrDefault();
					if (DX_F_Surcharge == null)
					{
						throw new Exception($"zip:{zip} 获取DX_Freight基础运费失败，请确认");
					}
					var fee = 0m;
					var formula = string.Empty;
					//莫有缘 根据计费重，向上取整，然后计算
					if (DX_F_Surcharge.IncType == "重量")
					{
						if (canAB)
						{
							var DXweight_vol = 0m;
							goods.ForEach(p =>
							{
								DXweight_vol += p.bzcd * p.bzgd * p.bzkd * p.qty / 5000;//计算DX体积重
							});
							var weight_sum = goods.Sum(p => p.weight_g / 1000 * p.qty);
							var billWeight = Math.Ceiling(DXweight_vol > weight_sum ? DXweight_vol : weight_sum); //计费重
							if (billWeight > 0)
							{
								fee += (decimal)DX_F_Surcharge.Fee;
								formula = $"{fee}";
							}
							if (billWeight - 20 > 0)
							{
								var weight_exceed_pre = billWeight > 100 ? 100 - 20 : billWeight - 20;
								fee += (decimal)DX_F_Surcharge.IncCoefficient1 * weight_exceed_pre;  ///这里是每千克*
								formula += $"+({(billWeight > 100 ? 100 : billWeight)}-20)*{(decimal)DX_F_Surcharge.IncCoefficient1}";
							}
							if (billWeight > 100)
							{
								var weight_exceed_pre = billWeight - 100;
								fee += (decimal)DX_F_Surcharge.IncCoefficient2 * weight_exceed_pre;///这里是每千克*
								formula += $"+({billWeight}-100)*{(decimal)DX_F_Surcharge.IncCoefficient2}";
							}
							formula += $"={fee}";
							result.serviceType = "DX_Freight_Merge";
							result.state = true;
						}
						else
						{
							var good = goods.First();
							var DXweight_vol = good.bzcd * good.bzgd * good.bzkd / 5000;//计算DX体积重
							var weight_sum = good.weight_g / 1000;
							var billWeight = Math.Ceiling(DXweight_vol > weight_sum ? DXweight_vol : weight_sum); //计费重
							if (billWeight > 0)
							{
								fee += (decimal)DX_F_Surcharge.Fee;
								formula = $"{fee}";
							}
							if (billWeight - 20 > 0)
							{
								var weight_exceed_pre = billWeight > 100 ? 100 - 20 : billWeight - 20;
								fee += (decimal)DX_F_Surcharge.IncCoefficient1 * weight_exceed_pre;  ///这里是每千克*
								formula += $"+({(billWeight > 100 ? 100 : billWeight)}-20)*{(decimal)DX_F_Surcharge.IncCoefficient1}";
							}
							if (billWeight > 100)
							{
								var weight_exceed_pre = billWeight - 100;
								fee += (decimal)DX_F_Surcharge.IncCoefficient2 * weight_exceed_pre;///这里是每千克*
								formula += $"+({billWeight}-100)*{(decimal)DX_F_Surcharge.IncCoefficient2}";
							}
							formula += $"={fee}";
							result.serviceType = "DX_Freight_Split";
							result.state = true;
						}
					}
					else if (DX_F_Surcharge.IncType == "件")
					{
						if (canAB)
						{
							fee = (decimal)DX_F_Surcharge.Fee;
							formula = $"{fee}={fee}";
							result.serviceType = "DX_Freight_Merge";
							result.state = true;
						}
						else
						{
							fee = (decimal)DX_F_Surcharge.Fee + (goods.Sum(p => p.qty) - 1) * (decimal)DX_F_Surcharge.IncCoefficient1;
							formula = $"{fee} +({goods.Sum(p => p.qty)} - 1) * {DX_F_Surcharge.IncCoefficient1}  ={fee}";
							result.serviceType = "DX_Freight_Split";
							result.state = true;
						}
					}
					else
					{
						throw new Exception($"zip:{zip}  DX_Freight基础运费类型未对接，请联系IT");
					}
					result.matchfee = fee;
					result.message = formula;

					#region 注释 old 根据林加财刷的运费计算，不用自己算了
					//var baseCharge = 0m;
					//var formula = "";
					////p4 = 燃油附加费
					//var p4 = 1.12m;

					//if (canAB)
					//{
					//	baseCharge = 8.85m * p4;
					//	formula = $"8.85* {p4}={baseCharge}";

					//	result.serviceType = "DX_Freight_Merge";
					//	result.state = true;
					//}
					//else
					//{
					//	var qty = goods.Sum(p => p.qty);
					//	baseCharge = ((8.85m + 8.25m * (qty - 1))) * p4;
					//	formula = $"((8.85 + 8.25 * ({qty} - 1))) * {p4}={baseCharge}";

					//	result.serviceType = "DX_Freight_Split";
					//	result.state = true;
					//}
					//result.matchfee = baseCharge;
					//result.message = formula;
					#endregion
				}
				#region 注释 DX 2 Man 不用算运费了，符合DX的都发DX
				//else if (logicswayoid == 103)
				//{
				//	string formula = string.Empty;
				//	decimal fee = 0;
				//	List<string> chargeItems = new List<string>();
				//	string item = "";

				//	var calcW = Math.Ceiling(model.Weight); // model.GetCalcWeightOnKg(RatioVW, false);
				//	var v = model.Length * model.Width * model.Height / 1000000;  //体积
				//	var length =

				//	decimal baseCharge = 0;
				//	if (model.Length <= 400)
				//	{
				//		if (calcW <= 35)
				//		{
				//			if (v <= 0.5M)
				//			{
				//				baseCharge = 29M;
				//			}
				//			else if (v > 0.5M && v <= 1.12M)
				//			{
				//				baseCharge = 41M;
				//			}
				//			formula = $"{baseCharge}";
				//			chargeItems.Add($"基本运费={formula}");
				//		}
				//		else if (calcW <= 85)
				//		{
				//			if (v <= 0.5M)
				//			{
				//				baseCharge = 29M + (calcW - 35M);
				//				//item = $"基本运费=29+{calcW}-35={baseCharge}";
				//				formula = $"29+{calcW}-35";
				//				chargeItems.Add($"基本运费={formula}");
				//			}
				//			else if (v <= 1.12M)
				//			{
				//				baseCharge = 41;
				//				//item = $"基本运费={baseCharge}";
				//				formula = $"{baseCharge}";
				//				chargeItems.Add($"基本运费={formula}");
				//			}
				//			else
				//			{
				//				baseCharge = 41M + (v * 200 - 225M) * 0.25M;
				//				//item = $"基本运费=41+({v}*200-225)*0.25={baseCharge}";
				//				formula = $"41+({v}*200-225)*0.25";
				//				chargeItems.Add($"基本运费={formula}");
				//			}
				//		}
				//		else
				//		{
				//			if (v > 0.5M && v <= 1.12M)
				//			{
				//				baseCharge = 41M + (calcW - 85) * 0.45M;
				//				//item = $"基本运费=41+({calcW}-85)*0.45={baseCharge}";
				//				formula = $"41+({calcW}-85)*0.45";
				//				chargeItems.Add($"基本运费={formula}");
				//			}
				//			else if (v > 1.12M)
				//			{
				//				baseCharge = 41M + (v * 200 - 225M) * 0.25M;
				//				//item = $"基本运费=41+({v}*200-225)*0.25={baseCharge}";
				//				formula = $"41+({v}*200-225)*0.25";
				//				chargeItems.Add($"基本运费={formula}");
				//			}
				//		}
				//	}
				//	if (baseCharge > 0)
				//	{
				//		var sumformula = $"({formula})";
				//		var addFee1 = baseCharge * 0.0995M; //运输税
				//		sumformula += $"+(({formula})*0.0995)";
				//		//chargeItems.Add(new ChargeItemModel(addFee1, $"Carriage Levy ={baseCharge}*9.95% ={addFee1}", 2));

				//		var addFee2 = baseCharge * 0.1795M;//燃油附加费
				//		sumformula += $"+(({formula})*0.0.1795)";
				//		chargeItems.Add($"sumformula");
				//		//chargeItems.Add(new ChargeItemModel(addFee1, $"Fuel Supplement ={baseCharge}*17.95% ={addFee2}", 2));

				//		fee = (baseCharge + addFee1 + addFee2);
				//		return new Tuple<decimal, string, List<string>>(fee, sumformula, chargeItems);
				//	}
				//	else
				//	{
				//		return new Tuple<decimal, string, List<string>>(-1, "不符合DX发货规则，无法获取运费", chargeItems);
				//		//throw new Exception("不符合DX发货规则，无法获取运费");
				//	}
				//}
				#endregion
				//DPD
				else if (logicswayoid == 19)
				{
					var db = new cxtradeModel();
					var searchZip = GetGBusfulZip(zip);
					var surcharge = db.GB_Logistics_Surcharge.Where(p => p.LogisticsId == logicswayoid && (p.Zip == searchZip || string.IsNullOrEmpty(p.Zip))).OrderByDescending(p => p.Zone).FirstOrDefault();
					if (surcharge == null)
					{
						throw new Exception($"zip:{zip} 获取DPD基础运费失败，请确认");
					}
					else
					{
						var fee = 0m;
						string formula = string.Empty;
						if (canAB)
						{
							fee = (decimal)surcharge.Fee;
							formula = $"{fee}={fee}";
						}
						else
						{
							var qty = goods.Sum(p => p.qty);
							fee = (decimal)surcharge.Fee * qty;
							formula = $"{surcharge.Fee} * {qty} ={fee}";
						}

						result.matchfee = fee;
						result.message = formula;
						result.serviceType = "DPD";
						result.state = true;
					}
				}
				else
				{
					result.message = "暂不支持该物流的运费计算!";
					//throw new Exception("暂不支持该物流的运费计算！");
				}
			}
			catch (Exception ex)
			{
				result.message = ex.Message;
				result.state = false;
			}

			return result;
		}

		private void MatchReturnByAU(scb_xsjl Order, ref OrderMatchResult OrderMatch)
		{
			var db = new cxtradeModel();
			var help = new Helper();
			if (Order.temp10 == "NZ")
			{
				OrderMatch.Result.Message = string.Format("NZ 未对接过退货单，暂不支持匹配");
				return;
			}

			var sheets = db.scb_xsjlsheet.Where(f => f.father == Order.number).ToList();
			var IsMultiPackage = sheets.Count > 1 || sheets.Any(f => f.sl > 1);
			if (IsMultiPackage)
			{
				OrderMatch.Result.Message = string.Format("多包裹请手动派单");
				return;
			}

			var sheet = sheets.First();
			var sizes = new List<double?>();
			var good = db.N_goods.Where(g => g.cpbh == sheet.cpbh && g.country == "AU" && g.groupflag == 0).FirstOrDefault();
			sizes.Add(good.bzgd);
			sizes.Add(good.bzcd);
			sizes.Add(good.bzkd);

			var Weight = Math.Ceiling((double)good.weight / 1000);//实重
			var Volume = (double)(good.bzcd * good.bzgd * good.bzkd / 1000000);//体积
																			   //var Volume_Weight = Math.Ceiling(Volume * 250);//体积重
																			   //2022年11月2号调整 计费重=毛重，不再计算体积重，X=ROUNDUP(毛重,0) 
																			   //var BillingWeight = Weight;//计费重 //Weight > Volume_Weight ? Weight : Volume_Weight;//计费重
			var maxNum = sizes.Max();//最长边

			var realsize = db.scb_realsize.FirstOrDefault(p => p.country == "AU" && p.cpbh == sheet.cpbh);
			var realsizelist = new List<double>();
			double realVolume = 0;
			double realMaxSize = 0;
			if (realsize != null)
			{
				realsizelist.Add((double)realsize.bzkd);
				realsizelist.Add((double)realsize.bzcd);
				realsizelist.Add((double)realsize.bzgd);
				realVolume = (double)(realsize.bzcd * realsize.bzgd * realsize.bzkd / 1000000);//体积
				realMaxSize = realsizelist.Max();
			}

			//符合aupost标准的发这个 默认9.49 ，剩余发dfe
			OrderMatch.WarehouseID = 107;
			OrderMatch.WarehouseName = "AU02";
			OrderMatch.Result.State = 1;
			if (realsize == null || (realMaxSize > 113 || Weight > 22 || realVolume > 0.25))
			{
				OrderMatch.Logicswayoid = 69;
				OrderMatch.SericeType = "DirectFreight";
			}
			else
			{
				OrderMatch.Logicswayoid = 68;
				OrderMatch.SericeType = "ParcelPost";
			}

			var carrier = db.scb_returnOrder_carrier.FirstOrDefault(p => p.nid == Order.number);
			if (carrier != null)
			{
				if (carrier.warehouseid.HasValue && carrier.warehouseid > 0)
				{
					OrderMatch.WarehouseID = carrier.warehouseid.Value;
					OrderMatch.WarehouseName = db.scbck.First(p => p.number == carrier.warehouseid.Value).id;
				}
				if (carrier.logicswayoid.HasValue && carrier.logicswayoid > 0)
				{
					OrderMatch.Logicswayoid = carrier.logicswayoid.Value;
					OrderMatch.SericeType = carrier.servicetype;
				}
			}

		}

		private decimal CalculateTheoryFeeForAU(List<scb_xsjlsheet> sheets, string tocountry)
		{
			var db = new cxtradeModel();
			var theory_fee = 0m;

			#region 理论运费
			var sql = string.Empty;
			var cpbhstr = string.Empty;
			sheets.ForEach(p => { cpbhstr += $"'{p.cpbh}',"; });
			if (tocountry.ToUpper().Equals("NZ"))
			{
				sql = $"select Goods cpbh, sum(fee) fee  from scb_logisticsForproduct_au_new where LogisticsID=85 and Goods in ({cpbhstr.Substring(0, cpbhstr.Length - 1)})  group by goods";
			}
			else
			{
				sql = $" select Goods cpbh, sum(fee) fee  from scb_LogisticsForProduct where Goods in ({cpbhstr.Substring(0, cpbhstr.Length - 1)})  and country='AU'  group by goods";
			}
			var theoryFeeList = db.Database.SqlQuery<GoodsTheoryFee>(sql).ToList();

			foreach (var item in theoryFeeList)
			{
				var sheetsl = sheets.Where(p => p.cpbh == item.cpbh).Sum(p => p.sl);
				theory_fee += sheetsl * item.fee;
			}
			#endregion

			return theory_fee;
		}

		private void MatchWarehouseByAUV2(scb_xsjl Order, ref OrderMatchResult OrderMatch, ref decimal theory_fee, ref decimal customPay_fee, ref decimal match_fee, ref decimal other)
		{
			#region 初始化
			var db = new cxtradeModel();
			var help = new Helper();
			var Tocountry = Order.temp10;
			var datformat = DateTime.Now.ToString("yyyy-MM-dd");

			var sheets = db.scb_xsjlsheet.Where(f => f.father == Order.number).ToList();
			var IsMultiPackage = sheets.Count > 1 || sheets.Any(f => f.sl > 1);

			#endregion

			#region 指定物流
			//decimal AUPostTempAddRateWA = 1M;//2022-11-24 北京时间21点开始 临时增加40%的WA紧急附加费，所有目的地为WA（请包括大小写，全程，缩写，空格，拼写错误）的订单
			//if (DateTime.Now > Convert.ToDateTime("2022-11-24 21:00:00"))
			//{
			//    AUPostTempAddRateWA = 1.4M;
			//}

			bool IsFind = true;
			if (IsFind)
			{
				var order_carriers = db.Database.SqlQuery<scb_order_carrier>(string.Format("select * from scb_order_carrier where orderid='{0}' and dianpu='{1}' and oid is not null"
					, Order.OrderID, Order.dianpu)).ToList();
				if (order_carriers.Any())
				{
					var order_carrier = order_carriers.First();
					OrderMatch.Logicswayoid = (int)order_carrier.oid;
					OrderMatch.SericeType = order_carrier.ShipService;
					OrderMatch.WarehouseID = 107;
					OrderMatch.WarehouseName = "AU02";
					OrderMatch.Result.Message = string.Format("指定物流服务 {0}", order_carrier.ShipService);
					OrderMatch.Result.State = 1;
					//return;
				}
			}
			#endregion

			#region 黑名单
			if (Order.phone.Contains("424188713") || Order.zip == "3337" && Order.city.ToUpper().Contains("KURUNJANG") && Order.khname.ToLower().Contains("maryana") && (Order.adress.ToLower().Contains("street") || Order.address1.ToLower().Contains("street") || Order.adress.ToLower().Contains("24") || Order.adress.ToLower().Contains("colonus") || Order.adress.ToLower().Contains("st") || Order.address1.ToLower().Contains("24") || Order.address1.ToLower().Contains("colonus") || Order.address1.ToLower().Contains("st")))
			{
				OrderMatch.Result.State = 0;
				OrderMatch.Result.Message = "匹配错误,该订单风险单拦截。";
				return;
			}
			if (BlackAddress(Order.dianpu, Order.xspt, Order.khname, Order.adress, Order.address1, Order.city, Order.country, Order.email, Order.fkzh, Order.phone, Order.bz))
			{
				OrderMatch.Result.Message = $"退单率高，暂停单，请确认是否发货！";
				return;
			}
			#endregion

			#region WIA匹配指定物流
			if (Order.lywl.ToUpper().Equals("WIA"))
			{
				if (Order.temp3 == "8")
				{
					OrderMatch.Result.Message = string.Format("订单编号:{0}:WIA补发单，请手动派单", Order.number);
				}
				else if (Order.temp3 != "8")
				{
					if (Order.zsl > 1)
					{
						OrderMatch.Result.Message = string.Format("订单编号:{0}:WIA订单商品数量大于1，请拆单", Order.number);
						return;
					}
					else
					{
						var wiaorder = db.scb_xsjl.Where(a => a.OrderID == Order.OrderID && a.number != Order.number).FirstOrDefault();
						var sqlstr = string.Empty;

						if (wiaorder == null)
						{
							sqlstr = $@"select top 1 recommendedshipMethod from sfa_shipment where buyerOrderId = '{Order.OrderID}'";
						}
						else
						{
							sqlstr = $@"select top 1 recommendedshipMethod from sfa_shipment where buyerOrderId = '{Order.OrderID}' and trackingId!='{wiaorder.trackno}' ";
						}
						var wiamatch = db.Database.SqlQuery<WIAMatchResult>(sqlstr).FirstOrDefault();
						if (wiamatch != null)
						{
							if (wiamatch.recommendedshipMethod == "FRF_AU_HB")
							{
								OrderMatch.Result.State = 1;
								OrderMatch.WarehouseID = 107;
								OrderMatch.WarehouseName = "AU02";
								OrderMatch.Logicswayoid = 129;
								OrderMatch.SericeType = "FRF_WIA";
							}
							else if (wiamatch.recommendedshipMethod == "DT_AU_HB")
							{
								OrderMatch.Result.State = 1;
								OrderMatch.WarehouseID = 107;
								OrderMatch.WarehouseName = "AU02";
								OrderMatch.Logicswayoid = 128;
								OrderMatch.SericeType = "DT_WIA";
							}
							else if (wiamatch.recommendedshipMethod == "CPLEASE_AU_DOM_SAVER")
							{
								OrderMatch.Result.State = 1;
								OrderMatch.WarehouseID = 107;
								OrderMatch.WarehouseName = "AU02";
								OrderMatch.Logicswayoid = 133;
								OrderMatch.SericeType = "CP_WIA";
							}
						}
						else
						{
							OrderMatch.Result.State = 0;
							OrderMatch.Result.Message = "匹配错误,物流信息未查到。";
							return;
						}
						return;
					}
				}
			}
			#endregion


			#region 仓库匹配
			var restockCK_AU = db.scbck.Where(o => o.countryid == "09" && o.realname.Contains("restock")).Select(o => o.id).ToList();
			var ws = new Dictionary<string, string>();
			var sheetsgroup = sheets.Select(a => a.cpbh).Distinct().ToList();
			foreach (var item in sheetsgroup)
			{
				var qty = sheets.Where(a => a.cpbh == item).ToList().Sum(a => a.sl);
				var Sql = string.Format(@"select Warehouse,kcsl,tempkcsl,unshipped from (
select Warehouse, SUM(sl) kcsl,
isnull((select SUM(sl) tempkcsl from scb_tempkcsl where nowdate = '{2}' and ck = Warehouse and cpbh = '{0}'), 0) tempkcsl,
isnull((select SUM(qty)  from tmp_pickinglist_occupy where cangku = Warehouse and cpbh = '{0}'), 0) unshipped
from scb_kcjl_location a inner join scb_kcjl_area_cangw b
on a.Warehouse = b.Location and a.Location = b.cangw
where b.state = 0 and a.Disable = 0  and b.cpbh = '{0}' and b.location in('AU02','AU91') and a.LocationType<10
group by Warehouse) t where kcsl - tempkcsl -unshipped  >= {1}", item, qty, datformat);

				var Warehouse = "";
				List<OrderMatchEntityAU> AuWarehouseList = db.Database.SqlQuery<OrderMatchEntityAU>(Sql).ToList();
				Helper.Info(Order.number, "wms_api", JsonConvert.SerializeObject(AuWarehouseList));
				//20250919 戴婕 FDSAU店铺restock可发
				//20251114 余芳芳dropshop-au 不发restock
				//20251117 朱军德 澳洲：FDSAU-Marketing 这几个店铺不发 restock 
				if (AuWarehouseList.Any() && (Order.temp3 == "8" || Order.temp3 == "4" || Order.temp3 == "6" || Order.temp3 == "9" || Order.temp10 == "NZ" || Order.dianpu.ToLower() == "dropshop-au" || Order.dianpu.ToLower() == "fdsau-marketing"))///|| Order.dianpu.ToUpper() == "FDSAU"
				{
					//var restockck = db.scbck.Where(o => o.countryid == "09" && o.realname.Contains("restock")).ToList();
					//var restockIDs = restockck.Select(o => o.id).ToList();
					AuWarehouseList = AuWarehouseList.Where(o => !restockCK_AU.Contains(o.Warehouse)).ToList();
					var message = $"新西兰订单、补发、重发、自提、卡车运输、独立站的订单不发退件仓！";//FDSAU
					Helper.Info(Order.number, "wms_api", message);
					if (AuWarehouseList.Count == 0)
					{
						OrderMatch.Result.Message = message;
						return;
					}
				}
				List<string> WarehouseList = AuWarehouseList.Select(p => p.Warehouse).ToList();
				foreach (var warehouseItem in WarehouseList) //优先匹配restock仓库
				{
					if (restockCK_AU.Contains(warehouseItem))
					{
						Warehouse = warehouseItem;
						break;
					}
				}
				if (string.IsNullOrWhiteSpace(Warehouse))
				{
					Warehouse = WarehouseList.FirstOrDefault();
				}
				ws.Add(item, Warehouse);
			}

			#region 判断是否多件商品匹配到了不同的仓库
			List<string> allCK = new List<string>();
			foreach (var itemCK in ws)
			{
				allCK.Add(itemCK.Value);
			}
			allCK = allCK.Distinct().ToList();
			if (allCK.Count() > 1)
			{
				OrderMatch.Result.State = 0;
				OrderMatch.Result.Message = "多件商品匹配到了不同的仓库，请先拆单！";
				return;
			}
			#endregion

			if (!ws.Any(f => f.Value == null))
			{

				if (ws.Any(f => f.Value == "AU91"))
				{
					OrderMatch.WarehouseID = 137;
					OrderMatch.WarehouseName = "AU91";
				}
				else
				{
					OrderMatch.WarehouseID = 107;
					OrderMatch.WarehouseName = "AU02";
				}
				if (OrderMatch.Result.State == 1)
				{
					// 20231019 吴碧芸 澳洲匹配加急物流ExpressPost 只能单个发，匹配的时候就做提示拆单
					if (Order.zsl > 1 && OrderMatch.SericeType == "ExpressPost")
					{
						OrderMatch.Result.Message += " ,ExpressPost 不支持多包，建议拆单！";
						OrderMatch.Result.State = 0;
						return;
					}
					else
						return;
				}
			}
			else //有的商品没匹配到
			{
				OrderMatch.Result.State = 0;
				OrderMatch.Result.Message = "匹配错误,库存不足";
				return;
			}

			#endregion

			#region 物流判断

			customPay_fee = sheets.Sum(p => (decimal)p.ptyf);//客户支付
			theory_fee = CalculateTheoryFeeForAU(sheets, Order.temp10); //理论运费

			if (Order.temp10.ToUpper() == "NZ")
			{
				OrderMatch.Logicswayoid = 85;
				OrderMatch.SericeType = "ParcelPost";
				OrderMatch.Result.State = 1;
				OrderMatch.Result.Message = "-";
				if (IsMultiPackage)
				{
					OrderMatch.Result.State = 0;
					OrderMatch.Result.Message = "AUPost国际件多包更便宜建议拆单！";
					return;
				}
				var cpbh = sheets.FirstOrDefault().cpbh;
				var realsize = db.scb_realsize.FirstOrDefault(p => p.country == "AU" && p.cpbh == cpbh);
				if (realsize == null)
				{
					OrderMatch.Result.State = 0;
					OrderMatch.Result.Message = $"匹配错误,未查询到{cpbh} 真实尺寸！";
				}
				else
				{
					var good = db.N_goods.FirstOrDefault(p => p.country == "AU" && p.cpbh == cpbh && p.groupflag == 0);
					var weight = Math.Ceiling((double)good.weight / 1000);//实重 
					var realsizelist = new List<double>() { Convert.ToDouble(Math.Ceiling(Convert.ToDecimal(realsize.bzcd))), Convert.ToDouble(Math.Ceiling(Convert.ToDecimal(realsize.bzgd))), Convert.ToDouble(Math.Ceiling(Convert.ToDecimal(realsize.bzkd))) };
					var maxsize = realsizelist.Max();
					var sumshortsize = (realsizelist.Sum() - maxsize) * 2;
					//20231101 何雨梦 匹配，真实尺寸，允许的范围包含 临界值；
					if (maxsize > 105 || sumshortsize > 140 || weight > 20)
					{
						OrderMatch.Result.State = 0;
						OrderMatch.Result.Message = $"匹配错误,AUPost：发不了,产品:{cpbh} 最长边:{maxsize} 两短边之和*2:{sumshortsize} 重量:{weight} 超过尺寸限制！";
					}
				}
				//if (OrderMatch.Result.State == 1)
				//{
				//	var freightLossFee = theory_fee + customPay_fee;
				//	if (freightLossFee <= -10)
				//	{
				//		OrderMatch.Result.State = 0;
				//		OrderMatch.Result.Message = $"匹配失败，运费亏损大于10";
				//		Helper.Info(Order.number, "wms_api", $"{freightLossFee}(运费亏损)={theory_fee}(理论运费)+{customPay_fee}(客户支付)-0(匹配运费)");
				//	}
				//}
				return;
			}

			if (string.IsNullOrWhiteSpace(Order.zip))
			{
				OrderMatch.Result.State = 0;
				OrderMatch.Result.Message = "匹配错误,邮编不能为空！";
				return;
			}
			var zip = int.Parse(Order.zip);
			var BillingWeights_AUDFE_List = new List<string>();
			var Other = new List<string>();

			decimal AUPostFeeSum = 0;
			decimal AUDFEFeeSum = 0;
			decimal AUaramexFeeSum = 0;
			decimal TGAFeeSum_IPEC = 0;
			decimal TGAFeeSum_Priority = 0;

			var AUPostMsg = string.Empty;
			var AUDFEMsg = string.Empty;
			var AUarameMsg = string.Empty;
			var TGAMsg_Priority = string.Empty;
			var TGAMsg_IPEC = string.Empty;
			var IsAUPost = true;
			var IsAUDFE = true;
			var Isaramex = true;
			var IsTGA_Priority = true;
			var IsTGA_IPEC = true;
			var Volume_total = 0d;

			//20241031 何雨梦 补发 重发禁用新物流aramex
			if (Order.temp3 == "8" || Order.temp3 == "4")
			{
				Isaramex = false;
			}
			//20241101 何雨梦 temu 平台 匹配和自动发货都禁用aramex 但是保留直接派单
			if (Order.xspt.ToLower() == "temu")
			{
				Isaramex = false;
			}

			// 20231027 hym 872  DFE回复KANPA是正确的 ,得做个替换关系了 KANPI, NT 0872的订单进来 匹配DFE的时候，价卡费率要按照KANPA，申请面单的时候city也要改成KANPA， 如果发aupost则没关系
			// 20240112 hym Riverlea Park 邮编5120   DFE邮编没更新  其实把城市改到Buckland Park就能发货，以后这个城市，系统发DFE的话，申请跟踪号的时候做一个自动替换
			// 20240307 hym Upper MT GRAVATT=UPPER MOUNT GRAVATT 这个城市加一下自动替换
			var dfeCity = Order.city;
			var xsjlsheet = new List<xsjlsheet>();
			foreach (var item in sheets)
			{
				var mapsheet = MapperHelper.Mapper<xsjlsheet, scb_xsjlsheet>(item);
				xsjlsheet.Add(mapsheet);
			}

			var ReplaceCityInfo = help.SqlQuery<scb_Logistics_AUDFE_CityReplace>(db,
				@"select * from scb_Logistics_AUDFE_CityReplace where postcode={0} and City={1}", zip, Order.city).ToList().FirstOrDefault();
			if (ReplaceCityInfo != null)
			{
				dfeCity = ReplaceCityInfo.Replace_City;
			}

			#region DFE 邮编校验
			//附加费
			var Destination = help.SqlQuery<scb_Logistics_AUDFE_Destination>(db,
				@"select top 1 * from scb_Logistics_AUDFE_Destination where postcode={0} and suburb={1}", zip, dfeCity).ToList().FirstOrDefault();
			var DestinationFee = 0M;
			bool IsDFECannotCityZip = false;//DFE特殊城市邮编排除
			if ((Order.zip == "3833" && Order.city.ToUpper() == "VESPER") || (Order.zip == "2440" && Order.city.ToUpper() == "DONDINGALONG"))
			{
				IsDFECannotCityZip = true;
			}
			var CarrierPrice = help.SqlQuery<scb_Logistics_AUDFE_CarrierPrice>(db,
				@"select top 1 * from scb_Logistics_AUDFE_CarrierPrice where exists(
                  select 1 from scb_Logistics_AUDFE_PostCode where postcode = {0} and postcodename={1}
                  and scb_Logistics_AUDFE_PostCode.carrierzone = scb_Logistics_AUDFE_CarrierPrice.carrierzone
                  and(suburb = '' or suburb = postcodename)) order by ID desc", zip, dfeCity).ToList().FirstOrDefault();
			if (CarrierPrice == null)
			{
				IsAUDFE = false;
				//OrderMatch.Result.Message += $"AUDFE：发不了,邮编({Order.zip})或者城市({Order.city})找不到！";
				AUDFEMsg += $"发不了,邮编({Order.zip})或者城市({Order.city})找不到！";
			}
			else if (IsDFECannotCityZip == true)
			{
				IsAUDFE = false;
				//OrderMatch.Result.Message += $"AUDFE:不派送, 需要去就近仓库自提，请先和客人确认！";
				AUDFEMsg += $"不派送, 需要去就近仓库自提，请先和客人确认！";
			}
			#endregion

			#region TGE 邮编校验
			var TGE_IPEC_PostCodeinfo = help.SqlQuery<scb_Logistics_AUTGE_IPEC_BasicFee>(db,
				@"select A.* from scb_Logistics_AUTGE_IPEC_BasicFee a
					left join scb_Logistics_AUTGE_IPEC_PostCode b  on a.toZone = b.zone 
					where   postCode = {0} and town = {1}", zip, Order.city).ToList().FirstOrDefault();

			//db.Database.SqlQuery<scb_Logistics_AUTGE_IPEC_BasicFee>(
			//		$@"select A.* from scb_Logistics_AUTGE_IPEC_BasicFee a
			//		left join scb_Logistics_AUTGE_IPEC_PostCode b  on a.toZone = b.zone 
			//		where town = '{Order.city.ToUpper()}' and postCode = {zip}"
			//	).FirstOrDefault();
			//var TGE_IPEC_rasfee = db.scb_Logistics_AUTGE_IPEC_RESFee.Where(p => p.suburb == Order.city && p.postcode == zip).FirstOrDefault();//矿区邮编拦截
			if (TGE_IPEC_PostCodeinfo == null)
			{
				IsTGA_IPEC = false;
				//OrderMatch.Result.Message += $"TGE-IPEC：发不了,邮编({Order.zip})或者城市({Order.city})找不到！"; //矿区邮编拦截
				TGAMsg_IPEC += $"发不了,邮编({Order.zip})或者城市({Order.city})找不到！"; //矿区邮编拦截
			}

			var TGE_Priority_PostCodeinfo = db.Database.SqlQuery<scb_Logistics_AUTGE_Pri_PostCode>(
					$@"SELECT * FROM scb_Logistics_AUTGE_Pri_PostCode WHERE postcodeFrom <= {zip} AND postcodeTo >= {zip}"
				).ToList();
			if (!TGE_Priority_PostCodeinfo.Any())
			{
				IsTGA_Priority = false;
				//OrderMatch.Result.Message += $"TGE-Priority：发不了,邮编({Order.zip})找不到！"; //矿区邮编拦截
				TGAMsg_Priority = $"发不了,邮编({Order.zip})找不到！"; //矿区邮编拦截
			}

			#endregion

			//卡车货号
			List<string> Transportation = new List<string>() { "JL10003AU-KO-PRO", "JL10003AU-WU-PRO", "JL10036AU-BN", "JL10002AU-BK" };
			var transportcpbh = sheets.Where(a => Transportation.Contains(a.cpbh)).FirstOrDefault();
			if (transportcpbh != null)
			{
				OrderMatch.Result.State = 0;
				OrderMatch.Result.Message = $"此单存在卡车单货物{transportcpbh.cpbh},请手动处理!\r\n！";
				return;
			}

			#endregion

			//var State = db.Database.SqlQuery<string>(string.Format("select top 1 State from scb_Logistics_AUPost_PostCode where postcode1<={0} and {0}<=postcode2 ", zip)).ToList().FirstOrDefault();
			//if (string.IsNullOrEmpty(State))
			//	State = "";

			Volume_total = 0d;
			var matchList = new List<MatchFeeResultModel>();

			#region AUPost
			if (IsAUPost)
			{
				AuMatchFreight matchfreight = new AuMatchFreight()
				{
					logicswayoid = 68,
					zip = zip,
					city = Order.city,
					sheets = xsjlsheet,
					serviceType = "ParcelPost"
				};
				var parts = JsonConvert.SerializeObject(matchfreight);
				var matchfreigh = Freight(matchfreight);
				AUPostFeeSum += matchfreigh.FreightResult.FeeSum;
				AUPostMsg += matchfreigh.FreightResult.AUMsg;
				IsAUPost = matchfreigh.State;
				matchList.Add(new MatchFeeResultModel()
				{
					logicswayoid = matchfreight.logicswayoid,
					serviceType = matchfreight.serviceType,
					message = matchfreigh.FreightResult.AUMsg,
					matchfee = matchfreigh.FreightResult.FeeSum,
					state = matchfreigh.State,
					other = matchfreigh.FreightResult.other
				});
			}
			#endregion

			#region AUDFE
			if (IsAUDFE)
			{
				//附加费
				if (Destination != null)
					DestinationFee = (decimal)Destination.Amount;
				AuMatchFreight matchfreight = new AuMatchFreight()
				{
					logicswayoid = 69,
					RatePrice = (decimal)CarrierPrice.Rate,
					Extralong = DestinationFee,
					MinimumCost = CarrierPrice.MinimumCost,
					city = Order.city,
					BasicCharge = (decimal)CarrierPrice.BasicCharge,
					sheets = xsjlsheet,
					serviceType = "DirectFreight"
				};
				var parts = JsonConvert.SerializeObject(matchfreight);
				var matchfreigh = Freight(matchfreight);
				AUDFEFeeSum += matchfreigh.FreightResult.FeeSum;
				AUDFEMsg = matchfreigh.FreightResult.AUMsg;
				Volume_total += matchfreigh.FreightResult.Volume_total;
				matchList.Add(new MatchFeeResultModel()
				{
					logicswayoid = matchfreight.logicswayoid,
					serviceType = matchfreight.serviceType,
					message = matchfreigh.FreightResult.AUMsg,
					matchfee = matchfreigh.FreightResult.FeeSum,
					state = matchfreigh.State,
					other = matchfreigh.FreightResult.other
				});
			}
			#endregion
			#region AUAramex
			if (Isaramex)
			{
				AuMatchFreight matchfreight = new AuMatchFreight()
				{
					logicswayoid = 127,
					zip = zip,
					city = Order.city,
					sheets = xsjlsheet,
					serviceType = "aramex"
				};
				var parts = JsonConvert.SerializeObject(matchfreight);
				var matchfreigh = Freight(matchfreight);
				AUaramexFeeSum += matchfreigh.FreightResult.FeeSum;
				AUarameMsg += matchfreigh.FreightResult.AUMsg;
				Isaramex = matchfreigh.State;
				matchList.Add(new MatchFeeResultModel()
				{
					logicswayoid = matchfreight.logicswayoid,
					serviceType = matchfreight.serviceType,
					message = matchfreigh.FreightResult.AUMsg,
					matchfee = matchfreigh.FreightResult.FeeSum,
					state = matchfreigh.State,
					other = matchfreigh.FreightResult.other
				});
			}
			#endregion
			#region  AUTGE
			if (IsTGA_Priority)
			{
				AuMatchFreight matchfreight = new AuMatchFreight()
				{
					logicswayoid = 112,
					zip = zip,
					city = Order.city,
					statsa = Order.statsa,
					sheets = xsjlsheet,
					serviceType = "TGE-Priority"
				};
				var parts = JsonConvert.SerializeObject(matchfreight);
				var matchfreigh = Freight(matchfreight);
				TGAFeeSum_Priority += matchfreigh.FreightResult.FeeSum;
				TGAMsg_Priority += matchfreigh.FreightResult.AUMsg;
				IsTGA_Priority = matchfreigh.State;
				matchList.Add(new MatchFeeResultModel()
				{
					logicswayoid = matchfreight.logicswayoid,
					serviceType = matchfreight.serviceType,
					message = matchfreigh.FreightResult.AUMsg,
					matchfee = matchfreigh.FreightResult.FeeSum,
					state = matchfreigh.State,
					other = matchfreigh.FreightResult.other
				});
			}
			if (IsTGA_IPEC)
			{
				AuMatchFreight matchfreight = new AuMatchFreight()
				{
					logicswayoid = 112,
					zip = zip,
					city = Order.city,
					statsa = Order.statsa,
					sheets = xsjlsheet,
					serviceType = "TGE-IPEC"
				};
				var parts = JsonConvert.SerializeObject(matchfreight);
				var matchfreigh = Freight(matchfreight);
				TGAFeeSum_IPEC += matchfreigh.FreightResult.FeeSum;
				TGAMsg_IPEC += matchfreigh.FreightResult.AUMsg;
				IsTGA_IPEC = matchfreigh.State;
				matchList.Add(new MatchFeeResultModel()
				{
					logicswayoid = matchfreight.logicswayoid,
					serviceType = matchfreight.serviceType,
					message = matchfreigh.FreightResult.AUMsg,
					matchfee = matchfreigh.FreightResult.FeeSum,
					state = matchfreigh.State,
					other = matchfreigh.FreightResult.other
				});
			}

			#endregion

			Helper.Info(Order.number, "wms_api", $"匹配物流：{JsonConvert.SerializeObject(matchList)}");
			//排除匹配失败的物流
			matchList = matchList.Where(p => p.state).ToList();

			//aramex差价在2块以内，优先老物流
			var aramexMatch = matchList.Where(p => p.serviceType == "aramex" && p.state).FirstOrDefault();
			if (aramexMatch != null)
			{
				if (matchList.Where(p => p.state && (p.logicswayoid == 68 && p.matchfee - aramexMatch.matchfee < 2) || (p.logicswayoid == 69 && p.matchfee - aramexMatch.matchfee < 2)).Any())
				{
					matchList = matchList.Where(p => p.serviceType != "aramex").ToList();
					Helper.Info(Order.number, "wms_api", $"{aramexMatch.serviceType}差价在2块以内，优先老物流。{JsonConvert.SerializeObject(matchList)}"); //记下日志
				}
			}

			//20241124 何雨梦 TGE差价在5块以内，优先老物流
			//20241127 何雨梦 TGE差价在3块以内，优先老物流
			//20241130 何雨梦 TGE分配差价从3块继续降到2块
			//20241209 何雨梦 TGE的匹配差价需要提高到5块
			//20250527 何雨梦 TGE差价5块优先老物流关闭
			//var TGEMatchs = matchList.Where(p => p.serviceType.StartsWith("TGE") && p.state).ToList();
			//if (TGEMatchs.Count > 0)
			//{
			//	foreach (var item in TGEMatchs)
			//	{
			//		if (matchList.Where(p => p.state && (p.logicswayoid == 68 && p.matchfee - item.matchfee < 5) || (p.logicswayoid == 69 && p.matchfee - item.matchfee < 5)).Any())
			//		{
			//			matchList = matchList.Where(p => p.serviceType != item.serviceType).ToList();
			//			Helper.Info(Order.number, "wms_api", $"{item.serviceType}差价在5块以内，优先老物流。{JsonConvert.SerializeObject(matchList)}"); //记下日志
			//		}
			//	}

			//}

			var cheapestItem = matchList.Where(p => p.state).OrderBy(p => p.matchfee).FirstOrDefault();

			if (cheapestItem != null)
			{
				//20241126 何雨梦 aramex需要根据报价接口判断是否能打单，不能打单的订单，不匹配aramex
				if (cheapestItem.serviceType == "aramex")
				{
					var getquoteRes = GetAramexQuote(sheets, Order);
					if (!getquoteRes.success)
					{
						cheapestItem = matchList.Where(p => p.state && p.serviceType != "aramex").OrderBy(p => p.matchfee).FirstOrDefault();
						Helper.Info(Order.number, "wms_api", $"aramex 获取报价失败。{JsonConvert.SerializeObject(getquoteRes)}"); //记下日志
					}
				}
				if (cheapestItem != null)
				{
					other = cheapestItem.other;
					OrderMatch.Logicswayoid = cheapestItem.logicswayoid;
					OrderMatch.SericeType = cheapestItem.serviceType;
					OrderMatch.Result.Message += cheapestItem.message;
					OrderMatch.Result.State = 1;
					match_fee = cheapestItem.matchfee;
				}
			}
			OrderMatch.Result.Message = $@"AUPost:{AUPostFeeSum.ToString("F2")} {(AUPostFeeSum > 0 ? "" : AUPostMsg)};DFE:{AUDFEFeeSum.ToString("F2")} {(AUDFEFeeSum > 0 ? "" : AUDFEMsg)};aramex:{AUaramexFeeSum.ToString("F2")} {(AUaramexFeeSum > 0 ? "" : AUarameMsg)};TGE-Priority:{TGAFeeSum_Priority.ToString("F2")} {(TGAFeeSum_Priority > 0 ? "" : TGAMsg_Priority)};TGE-IPEC:{TGAFeeSum_IPEC.ToString("F2")} {(TGAFeeSum_IPEC > 0 ? "" : TGAMsg_IPEC)}";
			var msg = string.Format("AUPost:{0} {1};DFE:{2} {3};aramex:{4} {5};TGE-Priority:{6} {7};TGE-IPEC:{8} {9}"
				, AUPostFeeSum == 0 ? "" : AUPostFeeSum.ToString(), AUPostMsg, AUDFEFeeSum, AUDFEMsg, AUaramexFeeSum, AUarameMsg, TGAFeeSum_Priority, TGAMsg_Priority, TGAFeeSum_IPEC, TGAMsg_IPEC);
			Helper.Info(Order.number, "wms_api", msg); //记下日志

			if (OrderMatch.Result.State == 1)
			{
				if (Order.xszje > 1300 || Volume_total > 2)
				{
					OrderMatch.Result.State = 0;
					OrderMatch.Result.Message = $"总体积：{Volume_total},请确认是否发卡车 " + OrderMatch.Result.Message;
				}
				else if (IsMultiPackage && OrderMatch.Logicswayoid == 68)
				{
					OrderMatch.Result.State = 0;
					OrderMatch.Result.Message = "AUPost多包更便宜建议拆单 " + OrderMatch.Result.Message;
				}
				else if (IsMultiPackage && OrderMatch.Logicswayoid == 127)
				{
					OrderMatch.Result.State = 0;
					OrderMatch.Result.Message = "aramex 需要拆单！ " + OrderMatch.Result.Message;
				}
			}
		}

		public AramexQuoteResult GetAramexQuote(List<scb_xsjlsheet> sheets, scb_xsjl xsjl)
		{
			var result = new AramexQuoteResult() { success = false };
			try
			{
				var db = new cxtradeModel();

				var items = new List<QuoteCpInfo>();
				foreach (var sheet in sheets)
				{
					var good = db.N_goods.Where(p => p.cpbh == sheet.cpbh && p.country == "AU").FirstOrDefault();
					items.Add(new QuoteCpInfo()
					{
						Quantity = sheet.sl,
						Reference = sheet.cpbh,
						PackageType = "P",
						WeightDead = Math.Round((decimal)good.weight / 1000, 2),
						Length = (decimal)good.bzcd,
						Width = (decimal)good.bzkd,
						Height = (decimal)good.bzgd,
					});
				}

				var address = new Aramex_Address()
				{
					StreetAddress = string.IsNullOrEmpty(xsjl.address1) ? xsjl.adress : xsjl.adress + " " + xsjl.address1,
					Locality = xsjl.city,
					StateOrProvince = xsjl.statsa,
					PostalCode = xsjl.zip,
					Country = xsjl.country
				};

				var to = new ToInfoModel()
				{
					ContactName = xsjl.khname,
					PhoneNumber = xsjl.phone,
					Email = xsjl.email,
					Address = address
				};

				var model = new AramexQuoteModel()
				{
					Items = items,
					To = to
				};

				var token = db.Scb_LogisticsTokens.Where(p => p.logicswayoid == "127").Select(p => p.Token).FirstOrDefault();
				if (string.IsNullOrEmpty(token))
				{
					throw new Exception("没有找到aramex的token");
				}

				var request = new AramexQuoteRequest()
				{
					token = token,
					model = model
				};
				var url = "http://3.73.234.168:8807/api/Aramex/";
				var res = HttpHandle.Post(JsonConvert.SerializeObject(request), url, "Quote");
				try
				{
					var data = JsonConvert.DeserializeObject<AramexQuoteResult>(res);
					result = data;
				}
				catch
				{
					result.message = res;
				}

			}
			catch (Exception ex)
			{
				result.message = ex.Message;
			}
			return result;
		}

		/// <summary>
		/// Aramex报价查询测试
		/// </summary>
		/// <param name="sheets"></param>
		/// <param name="xsjl"></param>
		/// <returns></returns>
		public AramexQuoteResult GetAramexQuoteTest(List<scb_xsjlsheet> sheets, scb_xsjl xsjl)
		{
			var result = new AramexQuoteResult() { success = false };
			try
			{
				var db = new cxtradeModel();

				var items = new List<QuoteCpInfo>();
				foreach (var sheet in sheets)
				{
					var good = db.N_goods.Where(p => p.cpbh == sheet.cpbh && p.country == "AU").FirstOrDefault();
					items.Add(new QuoteCpInfo()
					{
						Quantity = sheet.sl,
						Reference = sheet.cpbh,
						PackageType = "P",
						WeightDead = Math.Round((decimal)good.weight / 1000, 2),
						Length = (decimal)good.bzcd,
						Width = (decimal)good.bzkd,
						Height = (decimal)good.bzgd,
					});
				}

				var address = new Aramex_Address()
				{
					StreetAddress = string.IsNullOrEmpty(xsjl.address1) ? xsjl.adress : xsjl.adress + " " + xsjl.address1,
					Locality = xsjl.city,
					StateOrProvince = xsjl.statsa,
					PostalCode = xsjl.zip,
					Country = xsjl.country
				};

				var to = new ToInfoModel()
				{
					ContactName = xsjl.khname,
					PhoneNumber = xsjl.phone,
					Email = xsjl.email,
					Address = address
				};

				var model = new AramexQuoteModel()
				{
					Items = items,
					To = to
				};
				model.Services.Add(new Services()
				{
					ServiceCode = "DELOPT",
					ServiceItemCode = "ATL"
				});


				var token = db.Scb_LogisticsTokens.Where(p => p.logicswayoid == "127").Select(p => p.Token).FirstOrDefault();
				if (string.IsNullOrEmpty(token))
				{
					throw new Exception("没有找到aramex的token");
				}

				//var request = new AramexQuoteRequest()
				//{
				//	token = token,
				//	model = model
				//};
				//var url = "http://3.73.234.168:8807/api/Aramex/";
				//var res = HttpHandle.Post(JsonConvert.SerializeObject(request), url, "Quote");
				var headers = new List<Header>() {
					new Header()
					{
						Key = "Authorization",
						Value ="Bearer "+ token
					}
				};
				var url = "https://api.aramexconnect.com.au/api/consignments/quote";
				var res = HttpHandle.Post(url, JsonConvert.SerializeObject(model), "application/json", headers);

				try
				{
					//var data = JsonConvert.DeserializeObject<AramexQuoteResult>(res);
					//result = data;

					var data = JsonConvert.DeserializeObject<QuoteResponse>(res);
					result.data = data;
					result.success = true;
				}
				catch
				{
					result.message = res;
				}

			}
			catch (Exception ex)
			{
				result.message = ex.Message;
			}
			return result;
		}

		/// <summary>
		/// 欧洲
		/// </summary>
		/// <param name="Order"></param>
		/// <param name="List"></param>
		/// <param name="sheets"></param>
		/// <param name="sheetItem"></param>
		/// <returns></returns>
		private List<OrderMatchEntity> FilterByLogistics(scb_xsjl Order, List<OrderMatchEntity> List, List<scb_xsjlsheet> sheets, scb_xsjlsheet sheetItem, ref string message)
		{
			var db = new cxtradeModel();
			//var sheets = db.scb_xsjlsheet.Where(f => f.father == Order.number).ToList();
			var dat = AMESTime.AMESNowDE;
			#region 筛选指定物流
			var order_carrier = db.scb_order_carrier.FirstOrDefault(c => c.OrderID == Order.OrderID && c.dianpu == Order.dianpu && c.oid != null);
			var order_carrierCdiscount = db.scb_order_carrier.FirstOrDefault(c => c.OrderID == Order.OrderID && c.dianpu == Order.dianpu && c.ShipService.ToUpper() == "EXP");
			var order_carrierByCheck = true;//指定物流是否生效
			var cod = false;//cod 服务波兰优先
			var ispickUP = false;

			//波兰 、没有跟踪号的 ，自物流打单,选便宜的
			if (Order.dianpu.ToLower() == "wayfair-de" && DateTime.Now >= new DateTime(2024, 7, 22, 0, 0, 0, 0))
			{
				if (order_carrier != null)
				{
					var warehouseId = int.Parse(order_carrier.temp);
					var ck = db.scbck.FirstOrDefault(p => p.number == warehouseId);
					if (ck.Origin == "PL")
					{
						List = List.Where(p => p.WarehouseName == "EU09" || p.WarehouseName == "EU11").ToList();
						order_carrierByCheck = false;
						if (!List.Any())
						{
							message += $"wayfair-de发波兰只能波兰仓发货；";
						}
						return List;
					}
					else if (ck.id == "EU10")
					{
						var fr_sericeTypeStr = "DHL_FR_Marl,interDHL_FR_Marl,DPD,GLS,GLS_FR_Marl".Split(',');
						List = List.Where(p => p.WarehouseName == "EU10" && fr_sericeTypeStr.Any(o => o == p.SericeType)).ToList();
						order_carrierByCheck = false;
						if (!List.Any())
						{
							message += $"wayfair-de自发货EU10，只能DHL_FR_Marl,interDHL_FR_Marl,DPD,GLS,GLS_FR_Marl物流；";
						}
						return List;
					}
					else
					{
						message += $"可用库存不符合wayfair-de的匹配规则；";
						List = new List<OrderMatchEntity>();
						return List;
					}
				}
				else
				{
					message += $"可用库存不符合wayfair-de的匹配规则；";
					List = new List<OrderMatchEntity>();
					return List;
				}
			}

			//收货国家波兰 但是波兰无货的
			if (Order.temp10 == "PL" && !List.Any(f => f.WarehouseCountry == "PL") && Order.xspt.ToUpper() != "OTTO")
			{
				order_carrierByCheck = false;
			}
			if (order_carrier != null && order_carrierByCheck)
			{
				ispickUP = order_carrier.servicetype == "Pickup";

				if (order_carrier.oid != 20 && order_carrier.oid != 34)
					List = List.Where(f => f.Logicswayoid == (int)order_carrier.oid).ToList();
				else
				{
					List = List.Where(f => ((f.Logicswayoid == 20 || f.Logicswayoid == 23) && f.WarehouseCountry != "PL")
					|| ((f.Logicswayoid == 20 || f.Logicswayoid == 23 || f.Logicswayoid == 66) && f.WarehouseCountry == "PL")
					 || (f.WarehouseCountry == "DE" && f.Logicswayoid == 31)).ToList();
				}
				cod = order_carrier.temp2 == "cod";
				foreach (var item in List)
				{
					if (item.Logicswayoid != 66 && item.Logicswayoid != 31)
						item.SericeType = order_carrier.ShipService;
				}
			}
			//货到付款主要是波兰仓库
			if (cod)
			{
				if (List.Any(f => f.WarehouseCountry == "PL"))
				{
					List.RemoveAll(f => f.WarehouseCountry != "PL");
				}
			}
			if (ispickUP && Order.temp10 == "PL")
			{
				if (List.Any(f => f.WarehouseCountry == "PL"))
				{
					List.RemoveAll(f => f.WarehouseCountry != "PL");
				}
			}

			#endregion

			#region 过滤多包不建议发的物流
			if (sheets.Count() > 1 || sheets.Any(f => f.sl > 1))
			{
				// 法国dpd 不能发多包
				var dpd = List.FirstOrDefault(f => f.WarehouseCountry == "FR" && f.Logicswayoid == 19);
				if (dpd != null)
					List.Remove(dpd);
				// 意大利brt不建议发多包
				var brt = List.FirstOrDefault(f => f.WarehouseCountry == "IT" && f.Logicswayoid == 52);
				if (brt != null)
					List.Remove(brt);
			}
			#endregion
			#region 兰地花Hellmann只发多包  20250311
			if (sheets.Count() == 1 || sheets.Any(f => f.sl == 1))
			{
				// Hellmann 只能发多包
				var Hellmann = List.FirstOrDefault(f => f.Logicswayoid == 62);
				if (Hellmann != null)
					List.Remove(Hellmann);
			}
			#endregion

			#region 排除因为疫情不能发的物流 20240821注释
			//var list = db.scb_feiyan_postcode.Where(e => e.country.ToUpper() == Order.temp10.ToUpper() && e.pingtai == "All" && e.status == 1).ToList();
			//if (list.Any())
			//{
			//	var BanLogicswayoid = new List<int>();
			//	foreach (var item2 in List.Select(f => f.Logicswayoid).Distinct())
			//	{
			//		var list3 = list.Where(f => f.Logistics == item2).ToList();
			//		if (list3.Any())
			//		{
			//			try
			//			{
			//				var zip = int.Parse(Order.zip);
			//				var IsCheck = list3.Any(l => l.start <= zip && zip <= l.end);

			//				if (IsCheck)
			//				{
			//					BanLogicswayoid.Add(item2);
			//				}
			//			}
			//			catch
			//			{
			//			}
			//		}
			//	}
			//	List = List.Where(f => !BanLogicswayoid.Any(g => g == f.Logicswayoid)).ToList();

			//}
			#endregion


			#region   20241105 张洁楠 因洪灾影响，ES部分邮编不能发GEL
			var ban_GEL_Rule = db.scb_WMS_MatchRule.FirstOrDefault(f => f.Type == "Ban_GEL" && f.State == 1);
			if (ban_GEL_Rule != null && List.Any(p => p.SericeType.ToUpper() == "GEL"))
			{
				var ban_GEL_Details = db.scb_WMS_MatchRule_Details.Where(p => p.RID == ban_GEL_Rule.ID).ToList();
				if (ban_GEL_Details.Any(p => p.Parameter == Order.zip))
				{
					List = List.Where(p => p.SericeType != "GEL").ToList();
					Helper.Info(Order.number, "wms_api", $"因洪灾影响，ES部分邮编不能发GEL");
					message = List.Count == 0 ? $"匹配失败，因洪灾影响，ES部分邮编不能发GEL" : "";
				}
			}
			#endregion

			if (List.Count == 0)
			{
				return List;
			}

			List = List.Where(p => p.SericeType != "DHL_BERLIN" && (p.Logicswayoid != 20 || p.Logicswayoid != 23)).ToList();

			var datenow = DateTime.Now.ToString("yyyy-MM-dd");
			if (datenow == "2024-09-29" && List.Any(p => p.SericeType == "BRT"))
			{
				List = List.Where(p => p.SericeType != "BRT").ToList();
			}

			//20240806 张洁楠 意大利BRT预计本周三8.7开始匹配发货，目前发货处于测试阶段，所以麻烦设置发货限制为5单，超过5单的发其他物流 ；
			//20240918 关闭每天5单限制
			//var date = DateTime.Now;
			//if (date > new DateTime(2024, 8, 7, 0, 0, 0, 0) && List.Any(p => p.SericeType == "BRT"))
			//{
			//	var startTime = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, 0);
			//	if (db.scb_xsjl.Where(p => p.country == "EU" && p.state == 0 && p.temp3 == "0" && p.filterflag >= 4 && p.newdateis > startTime && p.servicetype == "BRT").Count() >= 5)
			//	{
			//		List = List.Where(p => p.SericeType != "BRT").ToList();
			//	}
			//}
			//else
			//{
			//	List = List.Where(p => p.SericeType != "BRT").ToList();
			//}

			#region 销售平台是limango 不用SDA_PL_IT物流 
			//20240816 厉彦 销售平台是limango 不用SDA_PL_IT物流 
			//20240920 厉彦 imango平台上面没有BRT对应的物流商，传不了跟踪号。麻烦把BRT这个物流限制掉
			if (Order.xspt.ToUpper() == "LIMANGO" && List.Any(p => p.SericeType == "SDA_PL_IT" || p.SericeType == "BRT"))
			{
				List = List.Where(p => p.SericeType != "SDA_PL_IT" && p.SericeType != "BRT").ToList();
			}
			#endregion

			//20230904 冯慧慧 GoplusDE-Shein匹配发货规则和otto的是一样的  只能用德国的物流
			//20230907 冯慧慧 norma24 这家店铺的麻烦也设置下哦 发货规则跟OTTO和shein一样的 都是只发德国的物流
			//20231205 姚芳芳 店铺：GoplusDE-Shein ， Norma24 恢复物流：GLS_ Berlin
			//20240102 姚芳芳 德国仓库新增物流Rottbeck_DE，OTTO平台店铺（OTTO，komfort-OTTO）和norma24都可以用的
			var matchDEDianpus = new List<string>() { "NORMA24" };
			if ((Order.xspt.ToLower() == "check24" || matchDEDianpus.Contains(Order.dianpu.ToUpper())))
			{
				//List.RemoveAll(f => f.WarehouseCountry != "DE" && f.SericeType.Contains("GLS"));
				//Otto只处理以下仓库和物流
				//EU01 EU05的DHL gls
				//EU07 EU09 EU11的DHL_berlin GLS_Berlin GLS_Globalway
				//EU10的 GLS_FR_Marl DHL_FR_Marl
				List = List.Where(o =>
				((o.WarehouseName == "EU09" || o.WarehouseName == "EU07" || o.WarehouseName == "EU11") && (o.SericeType.ToUpper() == "DHL_BERLIN" || o.SericeType.ToUpper() == "DHLLARGE_BERLIN" || o.SericeType.ToUpper() == "GLS_BERLIN" || o.SericeType.ToUpper() == "GLS_GLOBALWAY")) ||
				((o.WarehouseName == "EU01" || o.WarehouseName == "EU05" || o.WarehouseName == "EU15") && (o.SericeType.ToUpper() == "DHL" || o.SericeType.ToUpper() == "GLS" || o.SericeType.ToUpper() == "UPS_DE" || o.SericeType.ToUpper() == "GEL" || o.SericeType == "Rottbeck_DE")) ||
				((o.WarehouseName == "EU10") && (o.SericeType.ToUpper() == "GLS_FR_MARL" || o.SericeType.ToUpper() == "DHL_FR_MARL"))).ToList();

				if (List.Count == 0)
				{
					message += $"可用库存，不符合店铺check24、NORMA24的匹配规则；";
					return List;
				}
			}
			//20231102 姚芳芳 OTTO 意大利仓请设置禁止发货。
			//20231130 姚芳芳 OTTO两个店铺（OTTO，KOMFORT-OTTO）禁用波兰仓库的GLS_Berlin,德国仓库的UPS_DE 设置为可用物流     // || o.SericeType.ToUpper() == "GLS_BERLIN" 
			//20240102 姚芳芳 德国仓库新增物流Rottbeck_DE，OTTO平台店铺（OTTO，komfort-OTTO）和norma24都可以用的
			//20240109 兰地花 加restock仓
			//20240301 姚芳芳 解禁物流：GLS_Berlin解禁开始时间：从2024.2.29开始。涉及平台：OTTO涉及店铺：OTTO， komfort-OTTO
			//20240925 姚芳芳 仓库：01， 05,新增OTTO平台的发货物流：KN_DE
			if (Order.xspt.ToUpper() == "OTTO")
			{
				//List = List.Where(o =>
				//((o.WarehouseName == "EU09" || o.WarehouseName == "EU07" || o.WarehouseName == "EU11") && (o.SericeType.ToUpper() == "DHL_BERLIN" || o.SericeType.ToUpper() == "DHLLARGE_BERLIN" || o.SericeType.ToUpper() == "GLS_GLOBALWAY")) ||
				//((o.WarehouseName == "EU01" || o.WarehouseName == "EU05") && (o.SericeType.ToUpper() == "DHL" || o.SericeType.ToUpper() == "GLS" || o.SericeType.ToUpper() == "UPS_DE" || o.SericeType.ToUpper() == "GEL" || o.SericeType == "Rottbeck_DE")) ||
				//((o.WarehouseName == "EU10") && (o.SericeType.ToUpper() == "GLS_FR_MARL" || o.SericeType.ToUpper() == "DHL_FR_MARL"))).ToList();

				var pl_warehouseAndRestock = new List<string>() { "EU09", "EU71", "EU07", "EU91", "EU11" };
				var de_warehouseAndRestock = new List<string>() { "EU01", "EU05", "EU97", "EU93", "EU15", "EU135" };
				var fr_warehouseAndRestock = new List<string>() { "EU10", "EU101" };
				var it_warehouseAndRestock = new List<string>() { "EU12", "EU128" };

				List = List.Where(o =>
				(pl_warehouseAndRestock.Contains(o.WarehouseName) && (o.SericeType.ToUpper() == "DHL_BERLIN" || o.SericeType.ToUpper() == "UPS_PL" || o.SericeType.ToUpper() == "GLS_BERLIN" || o.SericeType.ToUpper() == "GLS")) ||
				(de_warehouseAndRestock.Contains(o.WarehouseName) && (o.SericeType.ToUpper() == "DHL" || o.SericeType.ToUpper() == "GLS" || o.SericeType.ToUpper() == "UPS_DE" || o.SericeType.ToUpper() == "GEL" || o.SericeType.ToUpper() == "KN_DE")) ||
				(fr_warehouseAndRestock.Contains(o.WarehouseName) && (o.SericeType.ToUpper() == "GLS_FR_MARL" || o.SericeType.ToUpper() == "DHL_FR_MARL" || o.SericeType.ToUpper() == "GLS" || o.SericeType.ToUpper() == "DPD")) ||
				(it_warehouseAndRestock.Contains(o.WarehouseName) && (o.SericeType.ToUpper() == "GLS"))
				).ToList();
				bool hasItalyGls = List.Any(o => it_warehouseAndRestock.Contains(o.WarehouseName) && o.SericeType.ToUpper() == "GLS");
				bool hasNonItaly = List.Any(o => !it_warehouseAndRestock.Contains(o.WarehouseName));

				if (hasItalyGls && hasNonItaly)
				{
					// 排除意大利仓库的 GLS 项
					List = List.Where(o =>
						!it_warehouseAndRestock.Contains(o.WarehouseName) || o.SericeType.ToUpper() != "GLS"
					).ToList();
				}

				if (List.Count == 0)
				{
					message += $"可用库存不符合OTTO的匹配规则；";
					return List;
				}
			}

			//#endregion		

			#region 张洁楠 2022-10-26 Bellumeurfr - cdiscount这家店铺暂时不用DHL，包括DHL_Berlin / DHL_FR_Marl。2022-10-27再加入Relax4lifefr-Cdiscount
			//张洁楠 2022-12-08 恢复
			//张洁楠 2023-02-08 Bellumeur店铺暂时不使用DHL相关物流，包括DHL_Berlin/DHL_FR_Marl 
			//张洁楠 2023-03-10 加入GiantexFR-cdiscount店铺暂时不使用DHL相关物流，包括DHL_Berlin/DHL_FR_Marl 
			//张洁楠 2023-04-03 取消Bellumeur店铺暂时不使用DHL相关物流，包括DHL_Berlin/DHL_FR_Marl     
			//张洁楠 2023-04-28 恢复Bellumeur店铺暂时不使用DHL相关物流，包括DHL_Berlin/DHL_FR_Marl 
			//张洁楠 2023-09-12 FDSLeroymerlin店铺暂时不使用DHL相关物流，包括DHL_Berlin/DHL_FR_Marl  
			//张洁楠 2023-09-14 JohnsonStore店铺暂时不使用DHL相关物流，包括DHL_Berlin/DHL_FR_Marl
			//张洁楠 2023-10-06 针对JohnsonStore 这家店，麻烦恢复DHL物流，包括DHL_Berlin、DHL_FR_Marl
			//张洁楠 2023-10-06 针对GiantexFR-cdiscount 这家店，麻烦恢复DHL物流，包括DHL_Berlin、DHL_FR_Marl
			//张洁楠 20231102 针对Relax4lifefr-Cdiscount 这家店，麻烦恢复DHL物流
			//张洁楠 2024-08-13 FDSLeroymerlin店铺暂时不使用DHL相关物流包括DHL_Berlin/DHL_FR_Marl   关闭
			var notUseDHLDianpus = new List<string>() { "gymaxfr-cdiscount", "bellumeurfr-cdiscount" };
			if (notUseDHLDianpus.Contains(Order.dianpu.ToLower())) //Order.dianpu.ToLower() == "gymaxfr-cdiscount" || Order.dianpu.ToLower() == "relax4lifefr-cdiscount" || Order.dianpu.ToLower() == "giantexfr-cdiscount" || Order.dianpu.ToLower() == "bellumeurfr-cdiscount"
			{
				Helper.Info(Order.number, "wms_api", $"Gymaxfr-Cdiscount，Bellumeurfr-cdiscount店铺暂时不用DHL，包括DHL_Berlin/DHL_FR_Marl。");
				List = List.Where(o => !o.SericeType.ToUpper().Contains("DHL")).ToList();

				if (List.Count == 0)
				{
					message += $"Gymaxfr-Cdiscount，Bellumeurfr-cdiscount店铺暂时不用DHL，包括DHL_Berlin/DHL_FR_Marl";
					return List;
				}
			}
			#endregion

			#region 张洁楠 20230922 homasisDE-am店铺禁止使用GLS_PL_Milan发货 
			//张洁楠 20240115 FDVAM - SE店铺禁止使用GLS_PL_Milan发货
			//20240129 张洁楠 KomfortES-am店铺不发GLS_PL_Milan
			if (Order.dianpu.ToLower() == "homasisde-am" || Order.dianpu.ToLower() == "komfortes-am" || Order.dianpu == "FDVAM-SE")
			{
				Helper.Info(Order.number, "wms_api", $"{Order.dianpu}店铺禁止使用GLS_PL_Milan发货");
				List = List.Where(o => !o.SericeType.ToUpper().Contains("GLS_PL_MILAN")).ToList();

				if (List.Count == 0)
				{
					message += $"{Order.dianpu}店铺禁止使用GLS_PL_Milan发货";
					return List;
				}
			}

			#endregion

			#region 姚芳芳 2023-08-18  Mytoys平台无法上传DPD跟踪号，Mytoys平台无法上传DPD跟踪号
			//Mytoys平台无法上传DPD跟踪号    20230818姚芳芳
			if (Order.xspt == "Mytoys" && Order.dianpu == "Mytoys-fdsde")
			{
				Helper.Info(Order.number, "wms_api", $"平台Mytoys，店铺Mytoys-fdsde暂时不用DPD。");
				List = List.Where(o => !o.SericeType.ToUpper().Contains("DPD")).ToList();

				if (List.Count == 0)
				{
					message += $"平台Mytoys，店铺Mytoys-fdsde暂时不用DPD。";
					return List;
				}
			}
			#endregion

			#region 张洁楠 2022-11-04 欧洲相关货号暂时只用DHL发货
			var onlySendDHL_List = db.scb_match_sku_OnlySendDHL.Where(p => p.SKU != "").AsNoTracking().Select(p => p.SKU).ToList();
			if (onlySendDHL_List.Any() && onlySendDHL_List.Contains(sheetItem.sku))
			{
				Helper.Info(Order.number, "wms_api", $"该订单货号暂时只能发DHL，包括DHL_Berlin/DHL_FR_Marl。");
				List = List.Where(o => o.SericeType.ToUpper().Contains("DHL")).ToList();
				if (List.Count == 0)
				{
					message += $"该订单货号暂时只能发DHL，包括DHL_Berlin/DHL_FR_Marl。";
					return List;
				}
			}
			#endregion

			#region 2022-07-13 FDS开头的店铺不进行restock分配  2024-08-08 邵露露 提出关闭 2024-08-09 夏俊提FDS B2C不发restock
			if (Order.dianpu.ToUpper().StartsWith("FDS") && Order.xspt == "B2C" && Order.dianpu != "FDSPL")
			{
				var euRestockCKList = db.scbck.Where(c => c.countryid == "05" && c.realname.Contains("restock")).ToList();
				List<int> euRestockCKnums = euRestockCKList.Select(p => p.number).ToList();
				List = List.Where(p => !euRestockCKnums.Contains(p.WarehouseID)).ToList();
				//List = List.Where(p => p.WarehouseName != "EU71" && p.WarehouseName != "EU88" && p.WarehouseName != "EU91" && p.WarehouseName != "EU93" && p.WarehouseName != "EU95" && p.WarehouseName != "EU97" && p.WarehouseName != "EU101").ToList();
				if (List.Count == 0)
				{
					message += $"FDS开头的店铺不进行restock分配";
					return List;
				}
			}
			#endregion

			#region 20240815 邬凤君 FDSPL allegro货到付款调整，本地仓，哪个物流便宜发哪个
			if (Order.dianpu == "FDSPL")
			{
				var _carrier = db.scb_order_carrier.FirstOrDefault(c => c.dianpu == Order.dianpu && c.OrderID == Order.OrderID && !string.IsNullOrEmpty(c.temp1) && c.temp2 == "cod");
				if (_carrier != null)
				{
					List = List.Where(p => p.WarehouseName == "EU11" || p.WarehouseName == "EU09").ToList();
				}
			}
			if (Order.dianpu == "allegro")
			{
				var _carrier = db.scb_order_carrier.FirstOrDefault(c => c.dianpu == Order.dianpu && c.OrderID == Order.OrderID && !string.IsNullOrEmpty(c.temp1) && c.temp2 == "cod");
				if (_carrier != null)
				{
					List = List.Where(p => p.WarehouseName == "EU11" || p.WarehouseName == "EU09" || p.WarehouseName == "EU71").ToList();
				}
			}
			#endregion

			#region 张洁楠 2022-06-20 DreamadeFR-cdiscount这家店铺设置为优先从EU10发货，若EU10没货，再按照谁便宜谁发的原则进行匹配。
			// 张洁楠 2022-08-03 GiantexFR-cdiscount这家法国店铺的发货规则麻烦设置为：优先从EU10仓发货，如果EU10仓没货，再从其他仓库发货。
			// 张洁楠 2025-05-06 GiantexFR-cdiscount 店铺优先限制取消
			if (Order.dianpu.ToLower() == "dreamadefr-cdiscount")///* || Order.dianpu.ToLower() == "giantexfr-cdiscount"*/
			{
				if (List.Exists(p => p.WarehouseName == "EU10"))
				{
					List = List.Where(p => p.WarehouseName == "EU10").ToList();
					if (List.Count == 0)
					{
						message += $"DreamadeFR-cdiscount店铺优先从EU10发货";
						return List;
					}
				}
			}
			#endregion

			#region  张洁楠 2023-02-10 针对以下店铺的订单，麻烦优先从EU06仓发货，如果EU06仓没货再从其他仓库根据谁便宜谁发的原则发货
			List<string> eu06PriorShopList = new List<string> { "fdvam-it", "giantexit-am", "gymaxit-am", "relax4lifeit-am", "lifezealit-am", "dreamadeit-manomano", "giantexit-manomano", "goplusit-manomano", "manomano-it", "relaxit-mano", "grassit24" };
			if (eu06PriorShopList.Contains(Order.dianpu.ToLower()))
			{
				if (List.Exists(p => p.WarehouseName == "EU06" || p.WarehouseName == "EU88"))
				{
					List = List.Where(p => p.WarehouseName == "EU06" || p.WarehouseName == "EU88").ToList();
					if (List.Count == 0)
					{
						message += $"{Order.dianpu}店铺优先从EU06发货";
						return List;
					}
				}
			}
			#endregion


			#region EU09 DHL每日限量800.3.21起改成1600 


			if (List.Any(f => (f.WarehouseName == "EU09" || f.WarehouseName == "EU07" || f.WarehouseName == "EU11")))
			{

				//改为Eu07和EU09一起2000
				//张洁楠2022-04-20改为Eu07和EU09和EU11波兰仓07、09、11仓合起来DHL单量限制为2500 +GLS_Berlin单量限制为1600+Globalway_GLS单量限制为200
				//张洁楠2022-11-21 DHL_Berlin和GLS_Berlin取消单量限制
				/*
				if (List.Any(f => f.SericeType.Contains("DHL")))
				{
					//int LimitCount = 800;
					//if (DateTime.Now >= new DateTime(2022, 03, 21))
					//    LimitCount = 1600;
					//if (DateTime.Now >= new DateTime(2022, 04, 08))
					int LimitCount = 2500;
					var qty1 = db.Database.SqlQuery<int>(string.Format(@"select isnull(SUM(s.sl),0) from scb_xsjl x inner join scb_xsjlsheet s on 
			x.number = s.father
			left join scbck c on x.warehouseid = c.number
			left join scb_Logisticsmode d on x.logicswayoid = d.oid
			where country = 'EU'
			and x.state = 0 and filterflag >= 5 and filterflag <= 10
			and isnull(ismark, '') = 0
			and x.temp3 not in ('4', '7') and
			not exists(select 1 from scb_WMS_Pond where state = 0 and NID = x.number)
			and x.logicswayoid in (20, 23) and x.warehouseid in (109 , 63, 163)")).ToList().FirstOrDefault();

					var qty2 = db.Database.SqlQuery<int>(string.Format(
						@"select (select isnull(SUM(Qty),0) from scb_WMS_Pond where warehouse in ('EU09','EU07','EU11') and State=0 and Way='DHL')+
					(select isnull(SUM(Qty),0) from scb_WMS_Pond where warehouse in ('EU09','EU07','EU11') and State=1 and Way='DHL' and picktime>='{0}')",
						dat.ToString("yyyy-MM-dd"))).ToList().FirstOrDefault();
					if (qty1 + qty2 >= LimitCount)
						List.RemoveAll(f => (f.WarehouseName == "EU09" || f.WarehouseName == "EU07" || f.WarehouseName == "EU11") && f.SericeType.Contains("DHL"));
					Helper.Info(Order.number, "wms_api", "EU09,EU07,EU11 DHL:" + (qty1 + qty2));
				}
				*/
				//GLS_Berlin    //张洁楠2022-11-21 DHL_Berlin和GLS_Berlin取消单量限制
				/*
				if (List.Any(f => f.SericeType.Contains("GLS_Berlin")))
				{
					//2022-06-10 by 张洁楠 “把gIs_ berlin的单 量调整为每天200单”
					int LimitCount = 200;// 1600; 
					var qty1 = db.Database.SqlQuery<int>(string.Format(@"select isnull(SUM(s.sl),0) from scb_xsjl x inner join scb_xsjlsheet s on 
			x.number = s.father
			left join scbck c on x.warehouseid = c.number
			left join scb_Logisticsmode d on x.logicswayoid = d.oid
			where country = 'EU'
			and x.state = 0 and filterflag >= 5 and filterflag <= 10
			and isnull(ismark, '') = 0
			and x.temp3 not in ('4', '7') and
			not exists(select 1 from scb_WMS_Pond where state = 0 and NID = x.number)
			and x.logicswayoid in (65) and x.warehouseid in (109 , 63 , 163)")).ToList().FirstOrDefault();

					var qty2 = db.Database.SqlQuery<int>(string.Format(
						@"select (select isnull(SUM(Qty),0) from scb_WMS_Pond where warehouse in ('EU09','EU07','EU11') and State=0 and Way='GLS_Berlin')+
					(select isnull(SUM(Qty),0) from scb_WMS_Pond where warehouse in ('EU09','EU07','EU11') and State=1 and Way='GLS_Berlin' and picktime>='{0}')",
					   dat.ToString("yyyy-MM-dd"))).ToList().FirstOrDefault();

					 if (qty1 + qty2 >= LimitCount)
						 List.RemoveAll(f => (f.WarehouseName == "EU09" || f.WarehouseName == "EU07" || f.WarehouseName == "EU11") && f.SericeType.Contains("GLS_Berlin"));
					Helper.Info(Order.number, "wms_api", "EU09,EU07,EU11 GLS_Berlin:" + (qty1 + qty2));
				}
				*/
				//GLS_Globalway 张洁楠2022 - 04 - 14 09:43 要求 GLS_Globalway 限制200
				if (List.Any(f => f.SericeType.Contains("GLS_Globalway")))
				{
					int LimitCount = 200;
					var qty1 = db.Database.SqlQuery<int>(string.Format(@"select isnull(SUM(s.sl),0) from scb_xsjl x inner join scb_xsjlsheet s on 
x.number = s.father
left join scbck c on x.warehouseid = c.number
left join scb_Logisticsmode d on x.logicswayoid = d.oid
where country = 'EU'
and x.state = 0 and filterflag >= 5 and filterflag <= 10
and isnull(ismark, '') = 0
and x.temp3 not in ('4', '7') and
not exists(select 1 from scb_WMS_Pond where state = 0 and NID = x.number)
and x.logicswayoid in (66) and x.warehouseid in (109 , 63 , 163)")).ToList().FirstOrDefault();
					var qty2 = db.Database.SqlQuery<int>(string.Format(
						 @"select (select isnull(SUM(Qty),0) from scb_WMS_Pond where warehouse in ('EU09','EU07','EU11') and State=0 and Way='GLS_Globalway')+
                    (select isnull(SUM(Qty),0) from scb_WMS_Pond where warehouse in ('EU09','EU07','EU11') and State=1 and Way='GLS_Globalway' and picktime>='{0}')",
						 dat.ToString("yyyy-MM-dd"))).ToList().FirstOrDefault();
					if (qty1 + qty2 >= LimitCount)
						List.RemoveAll(f => (f.WarehouseName == "EU09" || f.WarehouseName == "EU07" || f.WarehouseName == "EU11") && f.SericeType.Contains("GLS_Globalway"));
					Helper.Info(Order.number, "wms_api", "EU09,EU07,EU11 GLS_Globalway:" + (qty1 + qty2));

					if (List.Count == 0)
					{
						message += " GLS_Globalway  EU09,EU07,EU11 单量限制不超过200";
						return List;
					}
				}
			}
			#endregion

			#region EU05>EU01
			if (List.Any(f => f.WarehouseName == "EU05") && List.Any(f => f.WarehouseName == "EU01"))
			{
				List.RemoveAll(f => f.WarehouseCountry == "EU01");
			}
			#endregion
			#region (EU07=EU09)>EU11
			if ((List.Any(f => f.WarehouseName == "EU07") || List.Any(f => f.WarehouseName == "EU09")) && List.Any(f => f.WarehouseName == "EU11"))
			{
				List.RemoveAll(f => f.WarehouseName == "EU11");
			}
			#endregion

			#region dhl顺序调整 1. 波兰 2 马尔 3 汉堡
			//if (List.Any(f => f.SericeType == "DHL" && f.WarehouseCountry == "PL") && List.Any(f => f.SericeType == "DHL" && f.WarehouseCountry == "DE"))
			//{
			//    List.RemoveAll(f => f.SericeType == "DHL" && f.WarehouseCountry == "DE");
			//}
			//if (List.Any(f => f.SericeType == "DHL" && f.WarehouseName == "EU01") && List.Any(f => f.SericeType == "DHL" && f.WarehouseName == "EU05"))
			//{
			//    List.RemoveAll(f => f.SericeType == "DHL" && f.WarehouseCountry == "EU01");
			//}
			#endregion

			#region dhl顺序调整 1. 汉堡 2 马尔 3 波兰
			if (List.Any(f => f.SericeType == "DHL" && f.WarehouseName == "EU01"))
			{
				if (List.Any(f => f.SericeType == "DHL" && f.WarehouseCountry == "PL"))
					List.RemoveAll(f => f.SericeType == "DHL" && f.WarehouseCountry == "PL");
				if (List.Any(f => f.SericeType == "DHL" && f.WarehouseName == "EU05"))
					List.RemoveAll(f => f.SericeType == "DHL" && f.WarehouseName == "EU05");
			}
			if (List.Any(f => f.SericeType == "DHL" && f.WarehouseCountry == "PL") && List.Any(f => f.SericeType == "DHL" && f.WarehouseName == "EU05"))
			{
				List.RemoveAll(f => f.SericeType == "DHL" && f.WarehouseCountry == "PL");
			}
			#endregion

			#region EU07>EU08
			if (List.Any(f => f.WarehouseName == "EU08") && List.Any(f => f.WarehouseName == "EU09"))
			{
				List.RemoveAll(f => f.WarehouseName == "EU09");
			}

			if (List.Any(f => f.WarehouseName == "EU07") && List.Any(f => f.WarehouseName == "EU08"))
			{
				if (List.Any(f => f.SericeType == "DHL" && f.WarehouseName == "EU08"))
					List.RemoveAll(f => f.SericeType == "DHL" && f.WarehouseName == "EU07");
				else
					List.RemoveAll(f => f.WarehouseName == "EU08");
			}
			if (false && cod && List.Any(f => f.WarehouseCountry == "PL"))
			{
				List.RemoveAll(f => f.WarehouseCountry != "PL");
			}


			#endregion

			#region 收货国家是法国的订单（除了CXS的订单之外），按照谁便宜谁发的原则来发货，另外，CXS的订单必须从法国仓发且选择法国本地的物流发
			//2022-04-15wjw收货国家是法国的优先从法国仓发货”这一限制取消，改为收货国家是法国的订单（除了CXS的订单之外），按照谁便宜谁发的原则来发货，另外，CXS的订单必须从法国仓发且选择法国本地的物流发
			if (Order.temp10 == "FR" && List.Any(f => f.WarehouseCountry == "FR") && order_carrierCdiscount != null)
			{
				var JohnsonStoreDeel = db.scb_WMS_MatchRule.FirstOrDefault(f => f.Type == "JohnsonStoreDeel");

				if (JohnsonStoreDeel != null)
				{
					var JohnsonStoreDianpu = db.scb_WMS_MatchRule_Details.Where(f => f.RID == JohnsonStoreDeel.ID).Select(o => o.Parameter).ToList();
					if (JohnsonStoreDianpu.Any() && JohnsonStoreDianpu.Contains(Order.dianpu))
					{
						List.RemoveAll(f => f.WarehouseCountry != "FR");
						if (List.Count == 0)
						{
							message += "CXS的订单必须从法国仓发且选择法国本地的物流发";
							return List;
						}
					}
				}
			}
			#endregion

			#region 排查restock运费超过30欧元的物流方式
			var ALLrestockCKList = "EU97,EU95,EU93,EU91,EU88,EU101,EU71".Split(',').ToList();
			if (List.Any(f => f.Fee >= 30 && ALLrestockCKList.Contains(f.WarehouseName)))
			{
				List.RemoveAll(f => f.Fee >= 30 && ALLrestockCKList.Contains(f.WarehouseName));
				Helper.Info(Order.number, "wms_api", $"存在restock超过30欧的物流，进行排除");
				if (List.Count == 0)
				{
					message += $"存在restock超过30欧的物流，不进行匹配";
					return List;
				}
			}
			#endregion

			#region shein平台对发货国的特殊要求 20240116 shein的相关店铺解除所有仓库和物流的限制
			//// 20231009 姚芳芳  GoplusFR-Shein麻烦设置成仅能从法国发货; 

			////IT：06米兰仓发货
			////ES: 德国、法国仓库发货（即EU 01 05 10)
			////SE: 德国、法国仓库发货（即EU 01 05 10)
			////PT: 德国、法国仓库发货（即EU 01 05 10)
			////NL: 德国、法国仓库发货（即EU 01 05 10)
			////IT：EU 07 / 09 / 11仓也可以发货，但仅限于GLS_PL_Milan这个物流
			////FP仓库（EU10），可发物流: GLS_FR_Marl ，DHL_FR_Marl
			////PL仓库（EU07, 09, 11), 可发物流: DHL_berlin, GLS_Berlin
			////DE仓库（EU01, 05) 物流不受限制

			//var canDeFrDianpus = new List<string>() { "GOPLUSDE-SHEIN", "GOPLUSFR-SHEIN", "GoplusSE-Shein", "GoplusPT-Shein", "GoplusNL-Shein", "GoplusES-Shein" };
			////if (Order.dianpu.ToLower() == "goplusfr-shein")
			////{
			////	var cangkus_fr = new List<string>() { "EU10" };
			////	List.RemoveAll(p => !cangkus_fr.Contains(p.WarehouseName));
			////	Helper.Info(Order.number, "wms_api", $"GoplusFR-Shein 仅能从法国发货：{JsonConvert.SerializeObject(List)}");
			////}
			////  20231009 姚芳芳 GoplusPL-Shein麻烦设置成仅能从波兰发货  20231225 并且不能用DHL_BERLIN  GLS_BERLIN发货
			//if (Order.dianpu.ToLower() == "gopluspl-shein")
			//{
			//	var cangkus_pl = new List<string>() { "EU07", "EU09", "EU11" };
			//	List = List.Where(p => cangkus_pl.Contains(p.WarehouseName) && p.SericeType.ToUpper() != "DHL_BERLIN" && p.SericeType.ToUpper() != "GLS_BERLIN").ToList();
			//	message = List.Count == 0 ? "匹配失败，不符合发货规则！" : "";
			//	Helper.Info(Order.number, "wms_api", $"GoplusPL-Shein 仅能从波兰发货，并且不能用DHL_Berlin,GLS_Berlin：{JsonConvert.SerializeObject(List)}");
			//}
			//else if (Order.dianpu.ToLower() == "goplusit-shein")
			//{
			//	var onlyGLS_PL_Milan_cangkus_it = new List<string>() { "EU07", "EU09", "EU11" };
			//	List = List.Where(p => p.WarehouseName == "EU06" || (onlyGLS_PL_Milan_cangkus_it.Contains(p.WarehouseName) && p.SericeType.ToUpper() == "GLS_PL_Milan")).ToList();
			//	Helper.Info(Order.number, "wms_api", $"GoplusIT-Shein 仅能从EU06发货,EU07/09/11仓也可以发货，但仅限于GLS_PL_Milan：{JsonConvert.SerializeObject(List)}");
			//	message = List.Count == 0 ? "匹配失败，不符合发货规则！" : "";
			//}
			////德国、法国发货的店铺 GoplusSE-Shein   GoplusPT-Shein  GoplusNL-Shein  GoplusES-Shein  德国、法国仓库发货（即EU 01 05 10)
			//else if (canDeFrDianpus.Any(p => p.ToLower() == Order.dianpu.ToLower()))
			//{
			//	List = List.Where(o =>
			//	((o.WarehouseName == "EU09" || o.WarehouseName == "EU07" || o.WarehouseName == "EU11") && (o.SericeType.ToUpper() == "DHL_BERLIN" || o.SericeType.ToUpper() == "InterDHL_berlin".ToUpper() || o.SericeType.ToUpper() == "DHLLARGE_BERLIN" || o.SericeType.ToUpper() == "InterDHLlarge_berlin".ToUpper() || o.SericeType.ToUpper() == "GLS_BERLIN")) ||
			//	((o.WarehouseName == "EU01" || o.WarehouseName == "EU05" || o.WarehouseName == "EU10"))).ToList();
			//	Helper.Info(Order.number, "wms_api", $" GoplusSE-Shein,GoplusPT-Shein,GoplusNL-Shein,GoplusES-Shein 能从EU01,EU05,EU10发货 从波兰发货，并且只能用DHL_Berlin,GLS_Berlin：{JsonConvert.SerializeObject(List)}");
			//	message = List.Count == 0 ? "匹配失败，不符合发货规则！" : "";
			//}
			#endregion


			#region Temu 发货限制 姚芳芳

			#region 20240802 新的，根据配置的上架sku限制发货仓和物流
			if (Order.xspt.ToLower() == "temu")
			{
				if (!Order.dianpu.StartsWith("FDS"))
				{
					var sqlstr = $@"select dtwi.* from erp2..Data_Temu_Inventory_Single_warehouseid  a
left join cxtrade.dbo.Data_Temu_WarehouseId dtwi on a.WarehouseId = dtwi.WarehouseId
where ProductSkuId ='{sheetItem.itemno}' and  StoreName =  '{Order.dianpu}'";
					var temu_WarehouseIds = db.Database.SqlQuery<Data_Temu_WarehouseId>(sqlstr).ToList();
					if (!temu_WarehouseIds.Any())
					{
						message += $"未获取到temu店铺上架sku：{sheetItem.itemno}的仓库信息，请联系运营";
						Helper.Info(Order.number, "wms_api", message);
						return new List<OrderMatchEntity>();
					}
					var tempList = new List<OrderMatchEntity>();
					var temuMsg = string.Empty;
					foreach (var temu_WarehouseId in temu_WarehouseIds)
					{
						if (temu_WarehouseId.Ways.ToLower().Contains("dhl_berlin"))
						{
							temu_WarehouseId.Ways += ",DHLLARGE_BERLIN,interdhl_berlin";
						}
						if (temu_WarehouseId.Ways.ToLower().Contains("dhl_fr_marl"))
						{
							temu_WarehouseId.Ways += ",interDHL_FR_Marl";
						}
						var ways = temu_WarehouseId.Ways.Split(',').ToList();
						tempList.AddRange(List.Where(p => p.WarehouseName == temu_WarehouseId.CompWareId && ways.Any(o => o.ToLower() == p.SericeType.ToLower())).ToList());
						temuMsg += $",仓库 {temu_WarehouseId.CompWareId} {temu_WarehouseId.WarehouseId},可用物流 {temu_WarehouseId.Ways}";
					}
					Helper.Info(Order.number, "wms_api", $"temu上架sku：{sheetItem.itemno}{temuMsg}：{JsonConvert.SerializeObject(List)}");
					if (!tempList.Any())
					{
						message = $"匹配失败，temu上架sku：{sheetItem.itemno}{temuMsg},无库存";
						return new List<OrderMatchEntity>();
					}
					else
					{
						List = tempList;
					}
				}
				else
				{
					var sqlstr = $"select distinct CompWareId,StoreName,ways from Data_Temu_WarehouseId where storename  = '{Order.dianpu}'";
					var WarehouseIds = db.Database.SqlQuery<Data_Temu_WarehouseId>(sqlstr).ToList();
					Helper.Info(Order.number, "wms_api", $"{Order.dianpu},temu配置的发货物流：{JsonConvert.SerializeObject(List)}");
					foreach (var item in WarehouseIds)
					{
						if (item.Ways.ToLower().Contains("dhl_berlin"))
						{
							item.Ways += ",DHLLARGE_BERLIN,interdhl_berlin";
						}
						if (item.Ways.ToLower().Contains("dhl_fr_marl"))
						{
							item.Ways += ",interDHL_FR_Marl";
						}
						var ways = item.Ways.Split(',').ToList();
						List.Where(p => p.WarehouseName == item.CompWareId && !ways.Any(o => o.ToLower() == p.SericeType.ToLower())).ToList().ForEach(x => { List.Remove(x); });
					}

				}
			}

			#endregion

			#region 20240429 姚芳芳 TEMU 发货限制   注释
			//var warehouses_de = "EU01,EU05".Split(',').ToList();
			//var warehouses_pl = "EU07,EU09,EU11".Split(',').ToList();
			//var warehouses_fr = "EU10".Split(',').ToList();
			//var warehouses_it = "EU06".Split(',').ToList();

			//if (Order.dianpu == "Temugymax-DE" || Order.dianpu == "Gymaxpt-DE" || Order.dianpu == "Gymaxmaster-DE" || Order.dianpu == "Gymaxflame-DE")
			//{
			//	List = List.Where(p => warehouses_de.Contains(p.WarehouseName)
			//				|| (warehouses_pl.Contains(p.WarehouseName) && (p.SericeType == "DHL_berlin" || p.SericeType.ToUpper() == "DHLLARGE_BERLIN" || p.SericeType.ToLower() == "interdhl_berlin" || p.SericeType == "GLS_Berlin"))
			//		   ).ToList();
			//	Helper.Info(Order.number, "wms_api", $"{Order.dianpu}从DE仓发货，不限制物流，从PL仓发货，只能用DHL_berlin, GLS_Berlin：{JsonConvert.SerializeObject(List)}");
			//	message = List.Count == 0 ? $"匹配失败，{Order.dianpu}从DE仓发货，不限制物流，从PL仓发货，只能用DHL_berlin, GLS_Berlin" : "";
			//}
			//if (Order.dianpu == "Gymaxmaster-FR" || Order.dianpu == "Gymaxpt-FR" || Order.dianpu == "Temugymax-FR")
			//{
			//	List = List.Where(p => p.WarehouseName == "EU10" && p.SericeType != "DHL_FR_Marl" && p.SericeType != "GLS_FR_Marl").ToList();
			//	Helper.Info(Order.number, "wms_api", $"{Order.dianpu}从EU10发货，禁发DHL_FR_Marl, GLS_FR_Marl：{JsonConvert.SerializeObject(List)}");
			//	message = List.Count == 0 ? $"匹配失败，{Order.dianpu}只能从EU10发货,禁发DHL_FR_Marl, GLS_FR_Marl！" : "";
			//}
			//if (Order.dianpu == "Temugymax-IT" || Order.dianpu == "Gymaxpt-IT")
			//{
			//	List = List.Where(p => p.WarehouseName == "EU06").ToList();
			//	Helper.Info(Order.number, "wms_api", $"{Order.dianpu}从EU06发货，发往意大利，可使用意大利仓库的任一物流：{JsonConvert.SerializeObject(List)}");
			//	message = List.Count == 0 ? $"匹配失败，{Order.dianpu}只能从EU06发货！" : "";
			//}
			//if (Order.dianpu == "Gymaxpt-PL" || Order.dianpu == "Temugymax-ES")
			//{
			//	List = List.Where(p => warehouses_pl.Contains(p.WarehouseName) && (p.SericeType == "DHL_berlin" || p.SericeType.ToUpper() == "DHLLARGE_BERLIN" || p.SericeType.ToLower() == "interdhl_berlin" || p.SericeType == "GLS_Berlin")).ToList();
			//	Helper.Info(Order.number, "wms_api", $"{Order.dianpu}从EU07,EU09,EU11发货，只能用DHL_berlin, GLS_Berlin这两个物流：{JsonConvert.SerializeObject(List)}");
			//	message = List.Count == 0 ? $"匹配失败，{Order.dianpu}只能从EU07,EU09,EU11发货,只能用DHL_berlin, GLS_Berlin这两个物流！" : "";
			//}

			////20240625 胡文馨  Gymaxpt-DE店铺  发货时优先匹配德国仓库
			//if (Order.dianpu == "Gymaxpt-DE" && List.Any(p => warehouses_de.Contains(p.WarehouseName)))
			//{
			//	List = List.Where(p => !warehouses_pl.Contains(p.WarehouseName)).ToList();
			//	Helper.Info(Order.number, "wms_api", $"{Order.dianpu}Gymaxpt-DE店铺,发货时优先匹配德国仓库：{JsonConvert.SerializeObject(List)}");
			//}

			#endregion

			#endregion

			#region  temu、shein 不用DHL_IT
			if ("temu,shein".Contains(Order.xspt.ToLower()) && List.Count > 0)
			{
				List = List.Where(p => p.SericeType != "DHL_IT").ToList();
				Helper.Info(Order.number, "wms_api", $"temu、shein 不用DHL_IT：{JsonConvert.SerializeObject(List)}");
				message = List.Count == 0 ? "匹配失败，temu、shein 不用DHL_IT" : "";
			}
			#endregion

			#region 兰地花 20240607 homasis店铺的KC55737系列餐桌椅不用米兰物流发
			if ("homasisDE-am,homasisES-am".Contains(Order.dianpu) && sheets.Any(p => "KC55737BN,KC55737GR,KC55737CF".Contains(p.cpbh)) && List.Count > 0)
			{
				List = List.Where(p => p.WarehouseName != "EU06" && p.WarehouseName != "EU88" && p.SericeType != "GLS_PL_Milan").ToList();
				Helper.Info(Order.number, "wms_api", $" homasis店铺的KC55737系列餐桌椅不用米兰物流发：{JsonConvert.SerializeObject(List)}");
				message = List.Count == 0 ? "匹配失败， homasis店铺的KC55737系列餐桌椅不要用米兰物流发" : "";
			}
			#endregion

			//restock调整
			if (Order.temp10 == "PL") //波兰，（兰地花2022-08-25）收货地是PL直接排除非本地的退货仓-本地的大仓和退货仓优先发，没货的话再发非本地仓库
			{
				var restockCKList_PL = "EU91,EU71".Split(',').ToList();
				if (List.Any(f => restockCKList_PL.Contains(f.WarehouseName)))
				{
					foreach (var item in List)
					{
						if (restockCKList_PL.Contains(item.WarehouseName))
						{
							item.Fee = 0;
						}
					}
					//List = List.Where(f => restockCKList_PL.Contains(f.WarehouseName)).ToList();
					var strs = string.Join(",", List.Select(o => o.WarehouseName));
					Helper.Info(Order.number, "wms_api", $"波兰优先本地Restock：{strs}");
				}
			}
			else
			{
				var restockCKList = "EU97,EU95,EU93,EU91,EU88,EU101,EU71".Split(',').ToList();
				if (List.Any(f => restockCKList.Contains(f.WarehouseName)))
				{
					foreach (var item in List)
					{
						if (restockCKList.Contains(item.WarehouseName))
						{
							item.Fee -= 1000;
						}
					}
					//List = List.Where(f => restockCKList.Contains(f.WarehouseName)).ToList();
					var strs = string.Join(",", List.Select(o => o.WarehouseName));
					Helper.Info(Order.number, "wms_api", $"非波兰国家优先Restock：{strs}");
				}
			}

			#region// 20241105 张洁楠 德国的订单，麻烦优先从德国仓库发货
			if (Order.temp10 == "DE" && List.Any(p => "EU01,EU05,EU97,EU93".Contains(p.WarehouseName)))
			{
				List = List.Where(p => "EU01,EU05,EU97,EU93".Contains(p.WarehouseName)).ToList();
				Helper.Info(Order.number, "wms_api", $"目的地 {Order.temp10}，德国的订单，优先从德国仓库发货。{JsonConvert.SerializeObject(List)}");
			}
			#endregion

			#region //20241106 张洁楠 法国的优先从法国仓库发货，若法国仓库有货且最终匹配到的物流是geodis时，需要重新调整匹配规则，按照所有仓库现有库存谁便宜谁发原则去匹配
			if (Order.temp10 == "FR" && List.Any(f => f.WarehouseCountry == "FR"))
			{
				if (List.Where(p => p.WarehouseCountry == "FR").OrderBy(p => p.Fee).Select(p => p.SericeType).First() != "GEODIS")
				{
					List = List.Where(p => p.WarehouseCountry == "FR").ToList();
					Helper.Info(Order.number, "wms_api", $"法国的优先从法国仓库发货：{JsonConvert.SerializeObject(List)}");
				}
			}
			#endregion

			#region  20230928 张洁楠 收货国家是波兰的，优先用波兰本地物流发货（本地物流不包括DHL_Berlin、GLS_Berlin、GLS_PL_Milan），没货的话再按照其他仓库谁便宜谁发的
			var plLogistics = new List<string>() { "hellmann", "ups_pl", "dpd", "gls" };
			if (Order.temp10 == "PL" && List.Any(p => plLogistics.Contains(p.SericeType)))
			{
				List.RemoveAll(p => !plLogistics.Contains(p.SericeType));
				Helper.Info(Order.number, "wms_api", $"波兰国家优先波兰本地物流发货：{JsonConvert.SerializeObject(List)}");
			}
			#endregion

			#region   20240724 朴米夏 米兰发的包裹 不要用 SDA_ITInternational 这个物流，投递时间会超过3个星期太长
			var ban_SDA_ITInterRule = db.scb_WMS_MatchRule.FirstOrDefault(f => f.Type == "Ban_SDA_ITInter");
			var ban_SDA_ITInterDetails = db.scb_WMS_MatchRule_Details.Where(p => p.RID == ban_SDA_ITInterRule.ID).ToList();
			if (ban_SDA_ITInterDetails.Any() && List.Any())
			{
				if (Order.temp10.ToUpper() != "IT" && List.Where(p => p.Logicswayoid == 117 && p.WarehouseName == "EU06").Count() > 0 && ban_SDA_ITInterDetails.Any(p => (p.Type == "Store" && p.Parameter.ToLower() == Order.dianpu.ToLower()) || (p.Type == "platform" && p.Parameter.ToLower() == Order.xspt.ToLower())))
				{
					List = List.Where(p => p.WarehouseName != "EU06" && p.Logicswayoid != 117).ToList();
					Helper.Info(Order.number, "wms_api", $"{Order.dianpu} 店铺不用SDA_IT ");
					message = List.Count == 0 ? $"匹配失败，{Order.dianpu} 店铺不用SDA_IT " : "";
				}
			}

			#endregion


			#region 修正错误dhl服务
			foreach (var item in List)
			{
				if (item.SericeType == "DHL" || item.SericeType == "DHLlarge")
				{
					if (item.WarehouseCountry != Order.temp10 && (item.Logicswayoid == 20 || item.Logicswayoid == 23) && item.WarehouseCountry != "PL")
					{
						item.Logicswayoid = 23;
						item.SericeType = "InterDHL";
					}
				}
			}
			#endregion

			//dhl 加价2欧元
			foreach (var item in List)
			{
				if (Order.temp10.ToUpper() != "DE" && (item.SericeType.ToUpper() == "INTERDHL" || item.SericeType.ToUpper() == "DHL_BERLIN" || item.SericeType.ToUpper() == "INTERDHL_BERLIN" || item.SericeType.ToUpper() == "DHL_FR_MARL" || item.SericeType.ToUpper() == "INTER_DHL_FR_MARL" || item.SericeType.ToUpper() == "INTERDHL_FR_MARL"))
				{
					Helper.Info(Order.number, "wms_api", $"国际件{item.SericeType},{item.WarehouseName},需要涨价2元,原价{item.Fee},现价{item.Fee + 2}");
					item.Fee = item.Fee + 2;
				}
				#region  Colissimo_PL_FR匹配设置   20231024 张洁楠，取消设置
				////20231019 张洁楠 Colissimo_PL_FR这个物流正式开启匹配之后，波兰中转物流（DHL_Berlin、GLS_Berlin、GLS_PL_Milan）的运费在原运费基础上+4欧之后再进行后续的匹配
				//var PLTransferSericeTypeList = new List<string>() { "DHL_BERLIN", "GLS_Berlin", "GLS_Berlin" };
				//if (Order.temp10.ToUpper() == "FR" && item.WarehouseCountry == "PL" && PLTransferSericeTypeList.Any(p => p.ToUpper() == item.SericeType.ToUpper()))
				//{
				//	Helper.Info(Order.number, "wms_api", $"波兰中转物流（DHL_Berlin、GLS_Berlin、GLS_PL_Milan）,{item.WarehouseName},需要涨价4元,原价{item.Fee},现价{item.Fee + 4}");
				//	item.Fee = item.Fee + 4;
				//}
				////20231019 张洁楠 Colissimo_PL_FR这个物流正式开启匹配之后，波兰中转物流（波兰GLS、波兰DPD、Hellmann、UPS_PL）的运费在原运费基础上+2欧之后再进行后续的匹配
				//var PLLocalSericeTypeList = new List<string>() { "GLS", "DPD", "Hellmann", "UPS_PL" };
				//if (Order.temp10.ToUpper() == "FR" && item.WarehouseCountry == "PL" && PLTransferSericeTypeList.Any(p => p.ToUpper() == item.SericeType.ToUpper()))
				//{
				//	Helper.Info(Order.number, "wms_api", $"波兰本地物流（波兰GLS、波兰DPD、Hellmann、UPS_PL）,{item.WarehouseName},需要涨价2元,原价{item.Fee},现价{item.Fee + 2}");
				//	item.Fee = item.Fee + 2;
				//}
				#endregion

			}
			//UPS
			//加价2欧元2022-10-19取消
			//foreach (var item in List)
			//{
			//    if (item.SericeType.ToUpper() == "UPS_PL")
			//    {
			//        Helper.Info(Order.number, "wms_api", $"UPS_PL{item.SericeType},{item.WarehouseName},控制单量，需要涨价500元,原价{item.Fee},现价{item.Fee + 500}");
			//        item.Fee = item.Fee + 500;
			//    }
			//}

			return List;
		}

		/// <summary>
		/// 欧洲
		/// </summary>
		/// <param name="Order"></param>
		/// <param name="List"></param>
		/// <param name="sheets"></param>
		/// <param name="sheetItem"></param>
		/// <returns></returns>
		private List<OrderMatchEntity> FilterByLogisticsV3(EUMatchModel model, List<OrderMatchEntity> List, EUMatchSheetModel sheetItem, ref string message)
		{
			var db = new cxtradeModel();
			//var sheets = db.scb_xsjlsheet.Where(f => f.father == Order.number).ToList();
			var dat = AMESTime.AMESNowDE;
			#region 筛选指定物流
			var order_carrier = db.scb_order_carrier.FirstOrDefault(c => c.OrderID == model.OrderID && c.dianpu == model.dianpu && c.oid != null);
			var order_carrierCdiscount = db.scb_order_carrier.FirstOrDefault(c => c.OrderID == model.OrderID && c.dianpu == model.dianpu && c.ShipService.ToUpper() == "EXP");
			var scbcks = db.scbck.Where(p => p.id.StartsWith("EU") && p.state == 1 && p.IsMatching == 1).ToList();
			var order_carrierByCheck = true;//指定物流是否生效
			var cod = false;//cod 服务波兰优先
			var ispickUP = false;

			//波兰 、没有跟踪号的 ，自物流打单,选便宜的
			if (model.dianpu.ToLower() == "wayfair-de" && DateTime.Now >= new DateTime(2024, 7, 22, 0, 0, 0, 0))
			{
				if (order_carrier != null)
				{
					var warehouseId = int.Parse(order_carrier.temp);
					var ck = db.scbck.FirstOrDefault(p => p.number == warehouseId);
					if (ck.Origin == "PL")
					{
						List = List.Where(p => p.WarehouseName == "EU09" || p.WarehouseName == "EU11").ToList();
						order_carrierByCheck = false;
						if (!List.Any())
						{
							message += $"wayfair-de发波兰只能波兰仓发货；";
						}
						return List;
					}
					else if (ck.id == "EU10")
					{
						var fr_sericeTypeStr = "DHL_FR_Marl,interDHL_FR_Marl,DPD,GLS,GLS_FR_Marl".Split(',');
						List = List.Where(p => p.WarehouseName == "EU10" && fr_sericeTypeStr.Any(o => o == p.SericeType)).ToList();
						order_carrierByCheck = false;
						if (!List.Any())
						{
							message += $"wayfair-de自发货EU10，只能DHL_FR_Marl,interDHL_FR_Marl,DPD,GLS,GLS_FR_Marl物流；";
						}
						return List;
					}
					else
					{
						message += $"可用库存不符合wayfair-de的匹配规则；";
						List = new List<OrderMatchEntity>();
						return List;
					}
				}
				else
				{
					message += $"可用库存不符合wayfair-de的匹配规则；";
					List = new List<OrderMatchEntity>();
					return List;
				}
			}

			//20250508 兰地花 


			//收货国家波兰 但是波兰无货的
			if (model.temp10 == "PL" && !List.Any(f => f.WarehouseCountry == "PL") && model.xspt.ToUpper() != "OTTO")
			{
				order_carrierByCheck = false;
			}


			//20250508 兰地花 allegro 优先指定仓，指定仓没货发最优仓
			var allegroDianpus = "allegro,allegro-costway,goplus-allegro".Split(',').ToList();
			if (allegroDianpus.Any(p => p == model.dianpu.ToLower()) && order_carrier != null && order_carrier.oid > 0 && order_carrier.oid != 135 && order_carrier.oid != 53 && !List.Any(p => p.Logicswayoid == order_carrier.oid))
			{
				order_carrierByCheck = false;
			}

			//20250630 陈赛芬
			if (model.dianpu.ToLower() == "fdseu-tiktok" && List.Any(p => p.SericeType.ToUpper() == "BRT" && p.WarehouseName == "EU12"))
			{
				List.Remove(List.Where(p => p.SericeType == "BRT" && p.WarehouseName == "EU12").FirstOrDefault());
				Helper.Info(model.number, "wms_api", $"fdseu-tiktok 店 EU12不发BRT物流");
				message = List.Count == 0 ? $"匹配失败，fdseu-tiktok 店 EU12不发BRT物流" : "";
			}

			if (order_carrier != null && order_carrierByCheck)
			{
				ispickUP = order_carrier.servicetype == "Pickup";

				if (order_carrier.oid != 20 && order_carrier.oid != 34 && order_carrier.oid > 0)
				{
					List = List.Where(f => f.Logicswayoid == (int)order_carrier.oid).ToList();
				}
				else if (order_carrier.oid == 0 && !string.IsNullOrEmpty(order_carrier.temp))
				{
					try
					{
						var ckid = int.Parse(order_carrier.temp);
						if (List.Any(f => f.WarehouseID == ckid))
						{
							List = List.Where(f => f.WarehouseID == ckid).ToList();
						}
						else if ("Dropshop-EU,Dropshop-NewG".Contains(model.dianpu) && List.Any(f => f.Fee < 60 && f.WarehouseID != ckid))
						{
							List = List.Where(f => f.Fee < 60).ToList();
							Helper.Info(model.number, "wms_api", $"Dropshop-EU,Dropshop-NewG 指定仓没货运费小于60欧可发");
						}
						else
						{
							var hasStockList = List.Select(p => p.WarehouseName).Distinct().ToList();
							var ckname = scbcks.FirstOrDefault(p => p.number == ckid)?.name;
							message += $"指定仓：{ckname}无库存，请联系运营确认是否换仓！有库存的仓库：{string.Join(",", hasStockList)}；";
						}
					}
					catch
					{ }
				}
				else
				{
					List = List.Where(f => ((f.Logicswayoid == 20 || f.Logicswayoid == 23) && f.WarehouseCountry != "PL")
					|| ((f.Logicswayoid == 20 || f.Logicswayoid == 23 || f.Logicswayoid == 66) && f.WarehouseCountry == "PL")
					 || (f.WarehouseCountry == "DE" && f.Logicswayoid == 31)).ToList();
				}

				cod = order_carrier.temp2 == "cod";
				foreach (var item in List)
				{
					if (item.Logicswayoid != 66 && item.Logicswayoid != 31 && order_carrier.oid != 0)
						item.SericeType = order_carrier.ShipService;
				}
			}
			if (order_carrier != null && order_carrier.oid == 135)
			{

			}
			else
			{
				List = List.Where(a => a.SericeType != "UPS_PL_allegro").ToList();
			}
			//货到付款主要是波兰仓库
			if (cod)
			{
				if (List.Any(f => f.WarehouseCountry == "PL"))
				{
					List.RemoveAll(f => f.WarehouseCountry != "PL");
				}
			}
			if (ispickUP && model.temp10 == "PL")
			{
				if (List.Any(f => f.WarehouseCountry == "PL"))
				{
					List.RemoveAll(f => f.WarehouseCountry != "PL");
				}
			}

			#endregion

			#region 过滤多包不建议发的物流 
			if (model.sheets.Count() > 1 || model.sheets.Any(f => f.sl > 1))
			{
				// 法国dpd 不能发多包
				var dpd = List.FirstOrDefault(f => f.WarehouseCountry == "FR" && f.Logicswayoid == 19);
				if (dpd != null)
				{
					//List.Remove(dpd);
					List.ForEach(p =>
					{
						if (p.WarehouseCountry == "FR" && p.Logicswayoid == 19)
							p.isNeedSplit = true;
					});
				}

				// 意大利brt不建议发多包
				var brt = List.FirstOrDefault(f => f.WarehouseCountry == "IT" && f.Logicswayoid == 52);
				if (brt != null)
				{
					//List.Remove(brt);
					List.ForEach(p =>
					{
						if (p.WarehouseCountry == "IT" && p.Logicswayoid == 52)
							p.isNeedSplit = true;
					});
				}
			}
			#endregion
			#region 兰地花Hellmann只发多包  20250311
			if (model.sheets.Count() == 1 || model.sheets.Any(f => f.sl == 1))
			{
				// Hellmann 只能发多包
				var Hellmann = List.Where(f => f.Logicswayoid == 62).ToList();
				if (Hellmann.Count() > 0)
				{
					foreach (var item in Hellmann)
					{
						List.Remove(item);
					}
				}
			}
			#endregion

			#region 排除因为疫情不能发的物流 20240821注释
			//var list = db.scb_feiyan_postcode.Where(e => e.country.ToUpper() == Order.temp10.ToUpper() && e.pingtai == "All" && e.status == 1).ToList();
			//if (list.Any())
			//{
			//	var BanLogicswayoid = new List<int>();
			//	foreach (var item2 in List.Select(f => f.Logicswayoid).Distinct())
			//	{
			//		var list3 = list.Where(f => f.Logistics == item2).ToList();
			//		if (list3.Any())
			//		{
			//			try
			//			{
			//				var zip = int.Parse(Order.zip);
			//				var IsCheck = list3.Any(l => l.start <= zip && zip <= l.end);

			//				if (IsCheck)
			//				{
			//					BanLogicswayoid.Add(item2);
			//				}
			//			}
			//			catch
			//			{
			//			}
			//		}
			//	}
			//	List = List.Where(f => !BanLogicswayoid.Any(g => g == f.Logicswayoid)).ToList();

			//}
			#endregion

			#region   20241105 张洁楠 因洪灾影响，ES部分邮编不能发GEL
			var ban_GEL_Rule = db.scb_WMS_MatchRule.FirstOrDefault(f => f.Type == "Ban_GEL" && f.State == 1);
			if (ban_GEL_Rule != null && List.Any(p => p.SericeType.ToUpper() == "GEL"))
			{
				var ban_GEL_Details = db.scb_WMS_MatchRule_Details.Where(p => p.RID == ban_GEL_Rule.ID).ToList();
				if (ban_GEL_Details.Any(p => p.Parameter == model.zip))
				{
					List = List.Where(p => p.SericeType != "GEL").ToList();
					Helper.Info(model.number, "wms_api", $"因洪灾影响，ES部分邮编不能发GEL");
					message = List.Count == 0 ? $"匹配失败，因洪灾影响，ES部分邮编不能发GEL" : "";
				}
			}
			#endregion

			#region 20250514 张洁楠 部分邮编因为涉及塑料税禁发BRT
			//20250514 张洁楠 目的地为CANARIE（目的地为ES邮编为35/38开头的），CEUTA（目的地为ES邮编为510开头的），MELILLA（目的地为ES邮编为520开头的）因为涉及塑料税，所以禁发BRT；
			if (model.temp10 == "ES" && (model.zip.StartsWith("35") || model.zip.StartsWith("38") || model.zip.StartsWith("510") || model.zip.StartsWith("520")))
			{
				List = List.Where(p => p.SericeType != "BRT").ToList();
				Helper.Info(model.number, "wms_api", $"因涉及塑料税禁发BRT");
				message = List.Count == 0 ? $"匹配失败，因涉及塑料税禁发BRT" : "";
			}
			#endregion

			#region 20250523 张洁楠 目的地为意大利并且邮编属于Brescia的cap和Bergamo，麻烦禁发BRT；
			var IT_ZIP_banBrts = "25121,25125,25126,25127,25128,25122,25123,25124,25129,25131,25132,25133,25134,25135,25136,24100,24121,24122,24123,24124,24125,24126,24127,24128,24129".Split(',').ToList();
			if (model.temp10 == "IT" && IT_ZIP_banBrts.Any(p => p == model.zip) && List.Any(p => p.SericeType == "BRT"))
			{
				List = List.Where(p => p.SericeType != "BRT").ToList();
				Helper.Info(model.number, "wms_api", $"目的地为意大利并且邮编属于Brescia的cap和Bergamo，麻烦禁发BRT；");
			}
			#endregion

			#region 20250808 张洁楠 EU12发货的且目的地是ES并且邮编是07开头的，设置拦截
			if (List.Any(p => p.WarehouseName == "EU12") && model.temp10 == "ES" && model.zip.StartsWith("07"))
			{
				List = List.Where(p => p.WarehouseName != "EU12").ToList();
				Helper.Info(model.number, "wms_api", $"EU12发货的且目的地是ES并且邮编是07开头的，设置拦截");
				if (List.Count == 0)
				{
					message += $"EU12发货的且目的地是ES并且邮编是07开头的，设置拦截";
					return List;
				}
			}
			#endregion
			#region 20251014 姚芳芳 FDS-aliexpress店铺限制掉EU12仓 GLS物流禁止掉
			if (List.Any(p => p.WarehouseName == "EU12" && p.SericeType.ToUpper() == "GLS") && model.dianpu.ToLower() == "fds-aliexpress")
			{
				List = List.Where(p => !(p.SericeType.ToUpper() == "GLS" && p.WarehouseCountry == "IT")).ToList();
				Helper.Info(model.number, "wms_api", $"FDS-aliexpress店铺 EU12发货GLS物流禁止掉");
				if (List.Count == 0)
				{
					message += $"FDS-aliexpress店铺 EU12发货GLS物流禁止掉";
					return List;
				}
			}
			#endregion

			#region  20251121 张洁楠 Cdiscount平台下并且订单号开头是FYND的需要禁发UPS（所有的UPS都不行，UPS_DE\UPS_PL）
			//20251030 张洁楠 Cdiscount平台下并且订单号前四位是FYND的麻烦设置禁发BRT
			if (model.xspt.ToLower() == "cdiscount" && model.OrderID.StartsWith("FYND") && List.Any(p => p.SericeType.ToUpper() == "BRT" || p.SericeType.Contains("UPS")))
			{
				List.RemoveAll(p => p.SericeType.ToUpper() == "BRT" || p.SericeType.Contains("UPS"));
				Helper.Info(model.number, "wms_api", $"Cdiscount平台下并且订单号前四位是FYND的麻烦设置禁发BRT和UPS");
			}
			#endregion

			#region 20250530 张洁楠 以下地区因为价格高于原国家需要禁发意大利GLS
			if (List.Any(p => p.SericeType.ToUpper().Contains("GLS_PL_MILAN")))
			{
				var canzipsend_ITGls = true;
				if (model.temp10 == "PT" && model.zip.StartsWith("9"))
				{
					canzipsend_ITGls = false;
				}
				else if (model.temp10 == "FR" && model.zip.StartsWith("20"))
				{
					canzipsend_ITGls = false;
				}
				else if (model.temp10 == "ES")
				{
					//补足为 5 位，不够就前面补 '0'
					var zip = model.zip.PadLeft(5, '0');
					if (zip.StartsWith("07") || zip.StartsWith("35") || zip.StartsWith("38") || zip.StartsWith("51") || zip.StartsWith("52"))
					{
						canzipsend_ITGls = false;
					}
				}
				else if (model.temp10 == "GR")
				{
					var zip = int.Parse(model.zip.Replace(" ", ""));
					if (model.zip.StartsWith("49") || (zip >= 28000 && zip <= 31999) || (zip >= 70000 && zip <= 99999))
					{
						canzipsend_ITGls = false;
					}
				}
				if (!canzipsend_ITGls)
				{
					List = List.Where(p => !p.SericeType.ToUpper().Contains("GLS_PL_MILAN")).ToList();
					Helper.Info(model.number, "wms_api", $"邮编{model.zip} 因为价格高于原国家需要禁发意大利GLS");
				}
			}

			#endregion

			#region 20250610 张洁楠 部分邮编禁发波兰gls，因为涉及到岛屿附加费，比较贵
			if (List.Any(p => p.SericeType.ToUpper() == "GLS"))
			{
				var canzipsend_ITGls = true;
				if (model.temp10 == "PT")
				{
					try
					{
						var checkzip = int.Parse(model.zip.Replace(" ", "").Split('-').First());
						if (checkzip >= 9000 && checkzip <= 9999)
							canzipsend_ITGls = false;
					}
					catch { }

				}
				else if (model.temp10 == "FR")
				{
					try
					{
						var checkzip = int.Parse(model.zip);
						if (checkzip >= 20000 && checkzip <= 20621)
							canzipsend_ITGls = false;
					}
					catch { }
				}
				else if (model.temp10 == "ES")
				{
					try
					{
						var checkzip = int.Parse(model.zip);
						if (
							(checkzip >= 11700 && checkzip <= 11799)
							|| (checkzip >= 29800 && checkzip <= 29899)
							|| (checkzip >= 35000 && checkzip <= 35999)
							|| (checkzip >= 38000 && checkzip <= 38999)
							|| (checkzip >= 51000 && checkzip <= 52999)
							)
							canzipsend_ITGls = false;
					}
					catch { }
				}

				if (!canzipsend_ITGls)
				{
					List = List.Where(p => !(p.SericeType.ToUpper() == "GLS" && p.WarehouseCountry == "PL")).ToList();
					Helper.Info(model.number, "wms_api", $"邮编{model.zip} 禁发波兰gls，因为涉及到岛屿附加费，比较贵");
				}
			}
			#endregion

			if (model.temp10 == "FR" && List.Any(p => p.WarehouseCountry == "IT") && model.zip.StartsWith("20"))
			{
				List.RemoveAll(p => p.WarehouseCountry == "IT");
				Helper.Info(model.number, "wms_api", $"20250709 意大利仓库发往FR且邮编是20开头的所有物流全部禁发（因为属于法国的科西嘉岛）");
				if (List.Count == 0)
				{
					message += $"意大利仓库发往FR且邮编是20开头的所有物流全部禁发（因为属于法国的科西嘉岛）；";
					return List;
				}
			}
			if (model.temp10 == "FR" && List.Any(p => p.WarehouseCountry == "FR") && model.zip.StartsWith("20"))
			{
				List.RemoveAll(p => p.WarehouseCountry == "FR" && p.SericeType == "GEODIS");
				Helper.Info(model.number, "wms_api", $"20250709 法国仓库发往FR且邮编是20开头的禁发GEODIS（因为属于法国的科西嘉岛）");
				if (List.Count == 0)
				{
					message += $"法国仓库发往FR且邮编是20开头的禁发GEODIS（因为属于法国的科西嘉岛）；";
					return List;
				}
			}

			if (List.Count == 0)
			{
				return List;
			}

			List = List.Where(p => p.SericeType != "DHL_BERLIN" && (p.Logicswayoid != 20 || p.Logicswayoid != 23)).ToList();

			var datenow = DateTime.Now.ToString("yyyy-MM-dd");
			if (datenow == "2024-09-29" && List.Any(p => p.SericeType == "BRT"))
			{
				List = List.Where(p => p.SericeType != "BRT").ToList();
			}

			#region 法国GLS黑名单，涉及到的邮编及目的仓不能发
			var frglslimit = db.Database.SqlQuery<scb_WMS_MatchRule_Details>("select * from scb_WMS_MatchRule_Details where rid in (select id from scb_WMS_MatchRule where type = 'Ban_FrGls') ").ToList();
			if (frglslimit.Any() && frglslimit.Any(p => p.Type == model.temp10 && model.zip.StartsWith(p.Parameter)))
			{
				List.RemoveAll(p => p.SericeType == "GLS" && p.WarehouseCountry == "FR");
				Helper.Info(model.number, "wms_api", $"涉及岛屿附加费禁发法国GLS");
			}
			#endregion
			DateTime givenDate = new DateTime(2025, 5, 10, 0, 0, 0);
			if (List.Any(p => p.SericeType == "SDA_IT" && p.WarehouseCountry != "IT") && DateTime.Now > givenDate)
			{
				List.RemoveAll(p => p.SericeType.Contains("SDA_IT") && p.WarehouseCountry != "IT");
				Helper.Info(model.number, "wms_api", $"20250509 张洁楠SDA_IT 只有意大利本地开启");
			}
			#region  sda_PL_IT 每天的单量设置为最高1400  ； 20241210 张洁楠 sda_pl_it现在暂停掉匹配吧
			if (List.Any(p => p.SericeType == "SDA_PL_IT"))
			{
				List = List.Where(p => p.SericeType != "SDA_PL_IT").ToList();
				Helper.Info(model.number, "wms_api", $"20241210 张洁楠SDA_PL_IT卸货及发货速度慢，导致客诉量大幅度增加，所以现在开始暂停该物流的发货");

				//var tempDate = DateTime.Now;
				//var startTime = new DateTime(tempDate.Year, tempDate.Month, tempDate.Day, 0, 0, 0, 0);
				//var SDA_PL_IT_Limit = 1400;
				//if (db.scb_xsjl.Count(p => p.country == "EU" && p.state == 0 && p.temp3 == "0" && p.filterflag >= 4 && p.newdateis > startTime && p.logicswayoid == 125) >= SDA_PL_IT_Limit)
				//{
				//	List = List.Where(p => p.SericeType != "SDA_PL_IT").ToList();
				//}
			}
			#endregion
			//冯慧慧 扑米夏说ambro更便宜用ambro
			#region AMBRO尺寸限制
			if (List.Any(p => p.SericeType == "AmbroExpress"))
			{
				var ambro_allow_tocountrys = "PL,DE,CZ,AT".Split(',').ToList();
				if (ambro_allow_tocountrys.Any(p => p == model.temp10))
				{
					//var cpbh = model.sheets.First().cpbh;
					//var good = db.N_goods.FirstOrDefault(p => p.country == "EU" && p.cpbh == cpbh);
					//var girth = good.bzcd + 2 * (good.bzkd + good.bzgd);
					//var weight_kg = Math.Round((double)good.weight / 1000, 2);
					//if (girth <= 300 && weight_kg <= 31.5)
					//{
					//	List = List.Where(p => p.SericeType != "AmbroExpress").ToList();
					//}

				}
				else
				{
					List = List.Where(p => p.SericeType != "AmbroExpress").ToList();
				}
			}
			#endregion

			#region FDVAM-VC
			//20241024 余琳娜 总库存小于50的就不发货，提醒我们取消订单
			//20250305 兰地花 所有的订单只有12有货才从12发，vc暂时只有发到德国和波兰的
			if (model.dianpu == "FDVAM-VC")
			{
				if (List.Select(p => new { p.WarehouseID, p.kcsl, p.unshipped, p.unshipped2 }).Distinct().Sum(p => p.kcsl - p.unshipped - p.unshipped2) < 50)
				{
					message += $"FDVAM-VC店铺 库存小于50，请联系余琳娜取消订单；";
					List.Clear();
					return List;
				}
				//单个仓库库存>5
				List = List.Where(p => p.kcsl - p.unshipped - p.unshipped2 >= 5).ToList();
				if (List.Count == 0)
				{
					message += $"FDVAM-VC店铺 单个仓库库存>5,才能匹配；";
					return List;
				}

				//20251024 张洁楠 fdvam-vc该店铺需要禁发德国的gls、GLS_FR_Marl、GLS_berlin，
				if (List.Any(p => p.SericeType.Contains("GLS")))
				{
					List.RemoveAll(p => (p.Origin == "DE" && p.SericeType == "GLS") || p.SericeType == "GLS_FR_Marl" || p.SericeType == "GLS_berlin");
					if (List.Count == 0)
					{
						message += $"FDVAM-VC店铺禁发德国的gls、GLS_FR_Marl、GLS_berlin；";
						return List;
					}
				}


				if (List.Any(p => p.Origin == model.temp10))
				{
					List = List.Where(p => p.Origin == model.temp10).ToList();
				}
			}
			#endregion

			#region 20251024 张洁楠 tiktok 禁发brt
			if (model.xspt.ToLower() == "tiktok" && List.Any(p => p.SericeType == "BRT"))
			{
				List = List.Where(p => p.SericeType != "BRT").ToList();
				Helper.Info(model.number, "wms_api", $"tiktok 禁发brt");
			}

			#endregion

			#region 销售平台是limango 不用SDA_PL_IT物流  
			//20240816 厉彦 销售平台是limango 不用SDA_PL_IT物流 
			//20240920 厉彦 imango平台上面没有BRT对应的物流商，传不了跟踪号。麻烦把BRT这个物流限制掉
			//20250122 邵露露 销售平台是limango 不用EU12  
			//20250321 limango 可用EU12 不用BRT物流
			if (model.xspt.ToUpper() == "LIMANGO" && List.Any(p => p.SericeType == "BRT" && p.WarehouseName == "EU12"))
			{
				List = List.Where(p => p.SericeType != "BRT").ToList();
			}
			//20250521 厉彦  只FDS-limango店铺匹配的时候禁用SDA这个物流商
			if (model.dianpu.ToLower() == "fds-limango" && List.Any(p => p.SericeType.StartsWith("SDA")))
			{
				List.RemoveAll(p => p.SericeType.StartsWith("SDA"));
				Helper.Info(model.number, "wms_api", $"FDS-limango 店铺禁用SDA这个物流商");
			}
			#endregion

			#region 20250123 兰地花 收货国家为意大利的岛屿件，不发法国本地物流  scb_postcode_eu_islands
			if (model.temp10 == "IT")
			{
				var fr_localServices = "GLS,DPD,GEODIS,Chronopost,Colissimo".Split(',').ToList();
				//判断是否是小岛
				var sql = $"SELECT postcode FROM scb_postcode_eu_islands where country = 'IT' and postcode = '{model.zip}' and state = 0";
				var isIsland = db.Database.SqlQuery<string>(sql).FirstOrDefault();
				if (!string.IsNullOrEmpty(isIsland))
				{
					List.RemoveAll(a => a.WarehouseCountry == "FR" && fr_localServices.Contains(a.SericeType));
					Helper.Info(model.number, "wms_api", $"收货国家为意大利的岛屿件，不发法国本地物流：{JsonConvert.SerializeObject(List)}");
				}
			}
			#endregion


			//20250225 陈赛芬 Augenstern24-DE   SP37064  这个SKU 麻烦可以设置优先从波兰发货
			if (model.dianpu == "Augenstern24-DE" && model.sheets.Any(p => p.cpbh == "SP37064") && List.Any(p => p.WarehouseCountry == "PL"))
			{
				List = List.Where(p => p.WarehouseCountry == "PL").ToList();
				Helper.Info(model.number, "wms_api", $"Augenstern24-DE 店铺 SP37064货号，优先从波兰发货");
			}


			//20230904 冯慧慧 GoplusDE-Shein匹配发货规则和otto的是一样的  只能用德国的物流
			//20230907 冯慧慧 norma24 这家店铺的麻烦也设置下哦 发货规则跟OTTO和shein一样的 都是只发德国的物流
			//20231205 姚芳芳 店铺：GoplusDE-Shein ， Norma24 恢复物流：GLS_ Berlin
			//20240102 姚芳芳 德国仓库新增物流Rottbeck_DE，OTTO平台店铺（OTTO，komfort-OTTO）和norma24都可以用的
			var matchDEDianpus = new List<string>() { "NORMA24" };
			if ((model.xspt.ToLower() == "check24" || matchDEDianpus.Contains(model.dianpu.ToUpper())))
			{
				//List.RemoveAll(f => f.WarehouseCountry != "DE" && f.SericeType.Contains("GLS"));
				//Otto只处理以下仓库和物流
				//EU01 EU05的DHL gls
				//EU07 EU09 EU11的DHL_berlin GLS_Berlin GLS_Globalway
				//EU10的 GLS_FR_Marl DHL_FR_Marl
				List = List.Where(o =>
				((o.WarehouseName == "EU09" || o.WarehouseName == "EU07" || o.WarehouseName == "EU11") && (o.SericeType.ToUpper() == "DHL_BERLIN" || o.SericeType.ToUpper() == "DHLLARGE_BERLIN" || o.SericeType.ToUpper() == "GLS_BERLIN" || o.SericeType.ToUpper() == "GLS_GLOBALWAY")) ||
				((o.WarehouseName == "EU01" || o.WarehouseName == "EU05") && (o.SericeType.ToUpper() == "DHL" || o.SericeType.ToUpper() == "GLS" || o.SericeType.ToUpper() == "UPS_DE" || o.SericeType.ToUpper() == "GEL" || o.SericeType == "Rottbeck_DE")) ||
				((o.WarehouseName == "EU10") && (o.SericeType.ToUpper() == "GLS_FR_MARL" || o.SericeType.ToUpper() == "DHL_FR_MARL"))).ToList();

				if (List.Count == 0)
				{
					message += $"可用库存，不符合店铺check24、NORMA24的匹配规则；";
					return List;
				}
			}
			//20231102 姚芳芳 OTTO 意大利仓请设置禁止发货。
			//20231130 姚芳芳 OTTO两个店铺（OTTO，KOMFORT-OTTO）禁用波兰仓库的GLS_Berlin,德国仓库的UPS_DE 设置为可用物流     // || o.SericeType.ToUpper() == "GLS_BERLIN" 
			//20240102 姚芳芳 德国仓库新增物流Rottbeck_DE，OTTO平台店铺（OTTO，komfort-OTTO）和norma24都可以用的
			//20240109 兰地花 加restock仓
			//20240301 姚芳芳 解禁物流：GLS_Berlin解禁开始时间：从2024.2.29开始。涉及平台：OTTO涉及店铺：OTTO， komfort-OTTO
			//20240925 姚芳芳 仓库：01， 05,新增OTTO平台的发货物流：KN_DE
			if (model.xspt.ToUpper() == "OTTO")
			{
				//List = List.Where(o =>
				//((o.WarehouseName == "EU09" || o.WarehouseName == "EU07" || o.WarehouseName == "EU11") && (o.SericeType.ToUpper() == "DHL_BERLIN" || o.SericeType.ToUpper() == "DHLLARGE_BERLIN" || o.SericeType.ToUpper() == "GLS_GLOBALWAY")) ||
				//((o.WarehouseName == "EU01" || o.WarehouseName == "EU05") && (o.SericeType.ToUpper() == "DHL" || o.SericeType.ToUpper() == "GLS" || o.SericeType.ToUpper() == "UPS_DE" || o.SericeType.ToUpper() == "GEL" || o.SericeType == "Rottbeck_DE")) ||
				//((o.WarehouseName == "EU10") && (o.SericeType.ToUpper() == "GLS_FR_MARL" || o.SericeType.ToUpper() == "DHL_FR_MARL"))).ToList();
				var pl_warehouseAndRestock = new List<string>() { "EU09", "EU71", "EU07", "EU91", "EU11" };
				var de_warehouseAndRestock = new List<string>() { "EU01", "EU05", "EU97", "EU93", "EU15", "EU135" };
				var fr_warehouseAndRestock = new List<string>() { "EU10", "EU101" };
				var it_warehouseAndRestock = new List<string>() { "EU12", "EU128" };

				List = List.Where(o =>
				(pl_warehouseAndRestock.Contains(o.WarehouseName) && (o.SericeType.ToUpper() == "DHL_BERLIN" || o.SericeType.ToUpper() == "DHLLARGE_BERLIN" || o.SericeType.ToUpper() == "UPS_PL" || o.SericeType.ToUpper() == "GLS_BERLIN" || o.SericeType.ToUpper() == "GLS")) ||
				(de_warehouseAndRestock.Contains(o.WarehouseName) && (o.SericeType.ToUpper() == "DHL" || o.SericeType.ToUpper() == "GLS" || o.SericeType.ToUpper() == "UPS_DE" || o.SericeType.ToUpper() == "GEL" || o.SericeType.ToUpper() == "KN_DE")) ||
				(fr_warehouseAndRestock.Contains(o.WarehouseName) && (o.SericeType.ToUpper() == "GLS_FR_MARL" || o.SericeType.ToUpper() == "DHL_FR_MARL" || o.SericeType.ToUpper() == "GLS" || o.SericeType.ToUpper() == "DPD")) ||
				(it_warehouseAndRestock.Contains(o.WarehouseName) && (o.SericeType.ToUpper() == "GLS"))
				).ToList();
				bool hasItalyGls = List.Any(o => it_warehouseAndRestock.Contains(o.WarehouseName) && o.SericeType.ToUpper() == "GLS");
				bool hasNonItaly = List.Any(o => !it_warehouseAndRestock.Contains(o.WarehouseName));

				if (hasItalyGls && hasNonItaly)
				{
					// 排除意大利仓库的 GLS 项
					List = List.Where(o =>
						!it_warehouseAndRestock.Contains(o.WarehouseName) || o.SericeType.ToUpper() != "GLS"
					).ToList();
				}


				if (List.Count == 0)
				{
					message += $"可用库存不符合OTTO的匹配规则；";
					return List;
				}
			}

			//#endregion		

			#region 张洁楠 2022-10-26 Bellumeurfr - cdiscount这家店铺暂时不用DHL，包括DHL_Berlin / DHL_FR_Marl。2022-10-27再加入Relax4lifefr-Cdiscount
			//张洁楠 2022-12-08 恢复
			//张洁楠 2023-02-08 Bellumeur店铺暂时不使用DHL相关物流，包括DHL_Berlin/DHL_FR_Marl 
			//张洁楠 2023-03-10 加入GiantexFR-cdiscount店铺暂时不使用DHL相关物流，包括DHL_Berlin/DHL_FR_Marl 
			//张洁楠 2023-04-03 取消Bellumeur店铺暂时不使用DHL相关物流，包括DHL_Berlin/DHL_FR_Marl     
			//张洁楠 2023-04-28 恢复Bellumeur店铺暂时不使用DHL相关物流，包括DHL_Berlin/DHL_FR_Marl 
			//张洁楠 2023-09-12 FDSLeroymerlin店铺暂时不使用DHL相关物流，包括DHL_Berlin/DHL_FR_Marl  
			//张洁楠 2023-09-14 JohnsonStore店铺暂时不使用DHL相关物流，包括DHL_Berlin/DHL_FR_Marl
			//张洁楠 2023-10-06 针对JohnsonStore 这家店，麻烦恢复DHL物流，包括DHL_Berlin、DHL_FR_Marl
			//张洁楠 2023-10-06 针对GiantexFR-cdiscount 这家店，麻烦恢复DHL物流，包括DHL_Berlin、DHL_FR_Marl
			//张洁楠 20231102 针对Relax4lifefr-Cdiscount 这家店，麻烦恢复DHL物流
			//张洁楠 2024-08-13 FDSLeroymerlin店铺暂时不使用DHL相关物流包括DHL_Berlin/DHL_FR_Marl   关闭
			//张洁楠 2025-05-08 Bellumeurfr-cdiscount这家店，麻烦恢复DHL物流，包括DHL_Berlin、DHL_FR_Marl
			var notUseDHLDianpus = new List<string>() { "gymaxfr-cdiscount" };
			if (notUseDHLDianpus.Contains(model.dianpu.ToLower())) //Order.dianpu.ToLower() == "gymaxfr-cdiscount" || Order.dianpu.ToLower() == "relax4lifefr-cdiscount" || Order.dianpu.ToLower() == "giantexfr-cdiscount" || Order.dianpu.ToLower() == "bellumeurfr-cdiscount"
			{
				Helper.Info(model.number, "wms_api", $"Gymaxfr-Cdiscount店铺暂时不用DHL，包括DHL_Berlin/DHL_FR_Marl。");
				List = List.Where(o => !o.SericeType.ToUpper().Contains("DHL")).ToList();

				if (List.Count == 0)
				{
					message += $"Gymaxfr-Cdiscount店铺暂时不用DHL，包括DHL_Berlin/DHL_FR_Marl";
					return List;
				}
			}
			#endregion

			#region 张洁楠 20230922 homasisDE-am店铺禁止使用GLS_PL_Milan发货 
			//张洁楠 20240115 FDVAM - SE店铺禁止使用GLS_PL_Milan发货
			//20240129 张洁楠 KomfortES-am店铺不发GLS_PL_Milan
			if (model.dianpu.ToLower() == "homasisde-am" || model.dianpu.ToLower() == "komfortes-am" || model.dianpu == "FDVAM-SE")
			{
				Helper.Info(model.number, "wms_api", $"{model.dianpu}店铺禁止使用GLS_PL_Milan发货");
				List = List.Where(o => !o.SericeType.ToUpper().Contains("GLS_PL_MILAN")).ToList();

				if (List.Count == 0)
				{
					message += $"{model.dianpu}店铺禁止使用GLS_PL_Milan发货";
					return List;
				}
			}

			#endregion

			#region 姚芳芳 2023-08-18  Mytoys平台无法上传DPD跟踪号，Mytoys平台无法上传DPD跟踪号
			//Mytoys平台无法上传DPD跟踪号    20230818姚芳芳
			if (model.xspt == "Mytoys" && model.dianpu == "Mytoys-fdsde")
			{
				Helper.Info(model.number, "wms_api", $"平台Mytoys，店铺Mytoys-fdsde暂时不用DPD。");
				List = List.Where(o => !o.SericeType.ToUpper().Contains("DPD")).ToList();

				if (List.Count == 0)
				{
					message += $"平台Mytoys，店铺Mytoys-fdsde暂时不用DPD。";
					return List;
				}
			}
			#endregion

			#region 张洁楠 2022-11-04 欧洲相关货号暂时只用DHL发货
			var onlySendDHLSkuMatch = db.scb_WMS_MatchRule.Where(p => p.Type == "OnlySendDHLSku").FirstOrDefault();
			if (onlySendDHLSkuMatch != null)
			{
				var onlySendDHL_List = db.scb_WMS_MatchRule_Details.Where(p => p.RID == onlySendDHLSkuMatch.ID).Select(p => p.Parameter).ToList();
				if (onlySendDHL_List.Any() && onlySendDHL_List.Contains(sheetItem.sku))
				{
					Helper.Info(model.number, "wms_api", $"该订单货号暂时只能发DHL，包括DHL_Berlin/DHL_FR_Marl。");
					List = List.Where(o => o.SericeType.ToUpper().Contains("DHL")).ToList();
					if (List.Count == 0)
					{
						message += $"该订单货号暂时只能发DHL，包括DHL_Berlin/DHL_FR_Marl。";
						return List;
					}
				}
			}
			#endregion

			#region 2022-07-13 FDS开头的店铺不进行restock分配  2024-08-08 邵露露 提出关闭 2024-08-09 夏俊提FDS B2C不发restock
			if (model.dianpu.ToUpper().StartsWith("FDS") && model.xspt == "B2C" && model.dianpu != "FDSPL")
			{
				var euRestockCKList = db.scbck.Where(c => c.countryid == "05" && c.realname.Contains("restock")).ToList();
				List<int> euRestockCKnums = euRestockCKList.Select(p => p.number).ToList();
				List = List.Where(p => !euRestockCKnums.Contains(p.WarehouseID)).ToList();
				//List = List.Where(p => p.WarehouseName != "EU71" && p.WarehouseName != "EU88" && p.WarehouseName != "EU91" && p.WarehouseName != "EU93" && p.WarehouseName != "EU95" && p.WarehouseName != "EU97" && p.WarehouseName != "EU101").ToList();
				if (List.Count == 0)
				{
					message += $"FDS开头的店铺不进行restock分配";
					return List;
				}
			}
			#endregion

			#region 20240815 邬凤君 FDSPL allegro货到付款调整，本地仓，哪个物流便宜发哪个
			if (model.dianpu == "FDSPL")
			{
				var _carrier = db.scb_order_carrier.FirstOrDefault(c => c.dianpu == model.dianpu && c.OrderID == model.OrderID && !string.IsNullOrEmpty(c.temp1) && c.temp2 == "cod");
				if (_carrier != null)
				{
					List = List.Where(p => p.WarehouseName == "EU11" || p.WarehouseName == "EU09").ToList();
				}
			}
			if (model.dianpu == "allegro")
			{
				var _carrier = db.scb_order_carrier.FirstOrDefault(c => c.dianpu == model.dianpu && c.OrderID == model.OrderID && !string.IsNullOrEmpty(c.temp1) && c.temp2 == "cod");
				if (_carrier != null)
				{
					List = List.Where(p => p.WarehouseName == "EU11" || p.WarehouseName == "EU09" || p.WarehouseName == "EU71").ToList();
				}
			}
			#endregion

			#region by张洁楠 2022-06-20 DreamadeFR-cdiscount这家店铺设置为优先从EU10发货，若EU10没货，再按照谁便宜谁发的原则进行匹配。
			// by张洁楠 2022-08-03 GiantexFR-cdiscount这家法国店铺的发货规则麻烦设置为：优先从EU10仓发货，如果EU10仓没货，再从其他仓库发货。
			// 张洁楠 2025-05-07 GiantexFR-cdiscount 店铺优先限制取消
			if (model.dianpu.ToLower() == "dreamadefr-cdiscount")///* || Order.dianpu.ToLower() == "giantexfr-cdiscount"*/
			{
				if (List.Exists(p => p.WarehouseName == "EU10"))
				{
					List = List.Where(p => p.WarehouseName == "EU10").ToList();
					if (List.Count == 0)
					{
						message += $"DreamadeFR-cdiscount店铺优先从EU10发货";
						return List;
					}
				}
			}
			#endregion

			#region  张洁楠 2023-02-10 针对以下店铺的订单，麻烦优先从EU06仓发货，如果EU06仓没货再从其他仓库根据谁便宜谁发的原则发货
			List<string> eu06PriorShopList = new List<string> { "fdvam-it", "giantexit-am", "gymaxit-am", "relax4lifeit-am", "lifezealit-am", "dreamadeit-manomano", "giantexit-manomano", "goplusit-manomano", "manomano-it", "relaxit-mano", "grassit24" };
			if (eu06PriorShopList.Contains(model.dianpu.ToLower()))
			{
				if (List.Exists(p => p.WarehouseName == "EU06" || p.WarehouseName == "EU88"))
				{
					List = List.Where(p => p.WarehouseName == "EU06" || p.WarehouseName == "EU88").ToList();
					if (List.Count == 0)
					{
						message += $"{model.dianpu}店铺优先从EU06发货";
						return List;
					}
				}
			}
			#endregion


			#region EU09 DHL每日限量800.3.21起改成1600 


			if (List.Any(f => (f.WarehouseName == "EU09" || f.WarehouseName == "EU07" || f.WarehouseName == "EU11")))
			{

				//改为Eu07和EU09一起2000
				//张洁楠2022-04-20改为Eu07和EU09和EU11波兰仓07、09、11仓合起来DHL单量限制为2500 +GLS_Berlin单量限制为1600+Globalway_GLS单量限制为200
				//张洁楠2022-11-21 DHL_Berlin和GLS_Berlin取消单量限制
				/*
				if (List.Any(f => f.SericeType.Contains("DHL")))
				{
					//int LimitCount = 800;
					//if (DateTime.Now >= new DateTime(2022, 03, 21))
					//    LimitCount = 1600;
					//if (DateTime.Now >= new DateTime(2022, 04, 08))
					int LimitCount = 2500;
					var qty1 = db.Database.SqlQuery<int>(string.Format(@"select isnull(SUM(s.sl),0) from scb_xsjl x inner join scb_xsjlsheet s on 
		x.number = s.father
		left join scbck c on x.warehouseid = c.number
		left join scb_Logisticsmode d on x.logicswayoid = d.oid
		where country = 'EU'
		and x.state = 0 and filterflag >= 5 and filterflag <= 10
		and isnull(ismark, '') = 0
		and x.temp3 not in ('4', '7') and
		not exists(select 1 from scb_WMS_Pond where state = 0 and NID = x.number)
		and x.logicswayoid in (20, 23) and x.warehouseid in (109 , 63, 163)")).ToList().FirstOrDefault();

					var qty2 = db.Database.SqlQuery<int>(string.Format(
						@"select (select isnull(SUM(Qty),0) from scb_WMS_Pond where warehouse in ('EU09','EU07','EU11') and State=0 and Way='DHL')+
					(select isnull(SUM(Qty),0) from scb_WMS_Pond where warehouse in ('EU09','EU07','EU11') and State=1 and Way='DHL' and picktime>='{0}')",
						dat.ToString("yyyy-MM-dd"))).ToList().FirstOrDefault();
					if (qty1 + qty2 >= LimitCount)
						List.RemoveAll(f => (f.WarehouseName == "EU09" || f.WarehouseName == "EU07" || f.WarehouseName == "EU11") && f.SericeType.Contains("DHL"));
					Helper.Info(Order.number, "wms_api", "EU09,EU07,EU11 DHL:" + (qty1 + qty2));
				}
				*/
				//GLS_Berlin    //张洁楠2022-11-21 DHL_Berlin和GLS_Berlin取消单量限制
				/*
				if (List.Any(f => f.SericeType.Contains("GLS_Berlin")))
				{
					//2022-06-10 by 张洁楠 “把gIs_ berlin的单 量调整为每天200单”
					int LimitCount = 200;// 1600; 
					var qty1 = db.Database.SqlQuery<int>(string.Format(@"select isnull(SUM(s.sl),0) from scb_xsjl x inner join scb_xsjlsheet s on 
		x.number = s.father
		left join scbck c on x.warehouseid = c.number
		left join scb_Logisticsmode d on x.logicswayoid = d.oid
		where country = 'EU'
		and x.state = 0 and filterflag >= 5 and filterflag <= 10
		and isnull(ismark, '') = 0
		and x.temp3 not in ('4', '7') and
		not exists(select 1 from scb_WMS_Pond where state = 0 and NID = x.number)
		and x.logicswayoid in (65) and x.warehouseid in (109 , 63 , 163)")).ToList().FirstOrDefault();

					var qty2 = db.Database.SqlQuery<int>(string.Format(
						@"select (select isnull(SUM(Qty),0) from scb_WMS_Pond where warehouse in ('EU09','EU07','EU11') and State=0 and Way='GLS_Berlin')+
					(select isnull(SUM(Qty),0) from scb_WMS_Pond where warehouse in ('EU09','EU07','EU11') and State=1 and Way='GLS_Berlin' and picktime>='{0}')",
					   dat.ToString("yyyy-MM-dd"))).ToList().FirstOrDefault();

					 if (qty1 + qty2 >= LimitCount)
						 List.RemoveAll(f => (f.WarehouseName == "EU09" || f.WarehouseName == "EU07" || f.WarehouseName == "EU11") && f.SericeType.Contains("GLS_Berlin"));
					Helper.Info(Order.number, "wms_api", "EU09,EU07,EU11 GLS_Berlin:" + (qty1 + qty2));
				}
				*/
				//GLS_Globalway 张洁楠2022 - 04 - 14 09:43 要求 GLS_Globalway 限制200
				if (List.Any(f => f.SericeType.Contains("GLS_Globalway")))
				{
					int LimitCount = 200;
					var qty1 = db.Database.SqlQuery<int>(string.Format(@"select isnull(SUM(s.sl),0) from scb_xsjl x inner join scb_xsjlsheet s on 
x.number = s.father
left join scbck c on x.warehouseid = c.number
left join scb_Logisticsmode d on x.logicswayoid = d.oid
where country = 'EU'
and x.state = 0 and filterflag >= 5 and filterflag <= 10
and isnull(ismark, '') = 0
and x.temp3 not in ('4', '7') and
not exists(select 1 from scb_WMS_Pond where state = 0 and NID = x.number)
and x.logicswayoid in (66) and x.warehouseid in (109 , 63 , 163)")).ToList().FirstOrDefault();
					var qty2 = db.Database.SqlQuery<int>(string.Format(
						 @"select (select isnull(SUM(Qty),0) from scb_WMS_Pond where warehouse in ('EU09','EU07','EU11') and State=0 and Way='GLS_Globalway')+
                    (select isnull(SUM(Qty),0) from scb_WMS_Pond where warehouse in ('EU09','EU07','EU11') and State=1 and Way='GLS_Globalway' and picktime>='{0}')",
						 dat.ToString("yyyy-MM-dd"))).ToList().FirstOrDefault();
					if (qty1 + qty2 >= LimitCount)
						List.RemoveAll(f => (f.WarehouseName == "EU09" || f.WarehouseName == "EU07" || f.WarehouseName == "EU11") && f.SericeType.Contains("GLS_Globalway"));
					Helper.Info(model.number, "wms_api", "EU09,EU07,EU11 GLS_Globalway:" + (qty1 + qty2));

					if (List.Count == 0)
					{
						message += " GLS_Globalway  EU09,EU07,EU11 单量限制不超过200";
						return List;
					}
				}
			}
			#endregion

			#region EU05>EU01 
			//20241223 张洁楠  汉堡仓库优先于马尔仓库；
			if (List.Any(f => f.WarehouseName == "EU05") && List.Any(f => f.WarehouseName == "EU01"))
			{
				List.RemoveAll(f => f.WarehouseCountry == "EU05");
			}
			#endregion
			#region (EU07=EU09)>EU11
			if ((List.Any(f => f.WarehouseName == "EU07") || List.Any(f => f.WarehouseName == "EU09")) && List.Any(f => f.WarehouseName == "EU11"))
			{
				List.RemoveAll(f => f.WarehouseName == "EU11");
			}
			#endregion

			#region dhl顺序调整 1. 波兰 2 马尔 3 汉堡
			//if (List.Any(f => f.SericeType == "DHL" && f.WarehouseCountry == "PL") && List.Any(f => f.SericeType == "DHL" && f.WarehouseCountry == "DE"))
			//{
			//    List.RemoveAll(f => f.SericeType == "DHL" && f.WarehouseCountry == "DE");
			//}
			//if (List.Any(f => f.SericeType == "DHL" && f.WarehouseName == "EU01") && List.Any(f => f.SericeType == "DHL" && f.WarehouseName == "EU05"))
			//{
			//    List.RemoveAll(f => f.SericeType == "DHL" && f.WarehouseCountry == "EU01");
			//}
			#endregion

			#region dhl顺序调整 1. 汉堡 2 马尔 3 波兰
			if (List.Any(f => f.SericeType == "DHL" && f.WarehouseName == "EU01"))
			{
				if (List.Any(f => f.SericeType == "DHL" && f.WarehouseCountry == "PL"))
					List.RemoveAll(f => f.SericeType == "DHL" && f.WarehouseCountry == "PL");
				if (List.Any(f => f.SericeType == "DHL" && f.WarehouseName == "EU05"))
					List.RemoveAll(f => f.SericeType == "DHL" && f.WarehouseName == "EU05");
			}
			if (List.Any(f => f.SericeType == "DHL" && f.WarehouseCountry == "PL") && List.Any(f => f.SericeType == "DHL" && f.WarehouseName == "EU05"))
			{
				List.RemoveAll(f => f.SericeType == "DHL" && f.WarehouseCountry == "PL");
			}
			#endregion

			#region EU07>EU08
			if (List.Any(f => f.WarehouseName == "EU08") && List.Any(f => f.WarehouseName == "EU09"))
			{
				List.RemoveAll(f => f.WarehouseName == "EU09");
			}

			if (List.Any(f => f.WarehouseName == "EU07") && List.Any(f => f.WarehouseName == "EU08"))
			{
				if (List.Any(f => f.SericeType == "DHL" && f.WarehouseName == "EU08"))
					List.RemoveAll(f => f.SericeType == "DHL" && f.WarehouseName == "EU07");
				else
					List.RemoveAll(f => f.WarehouseName == "EU08");
			}
			if (false && cod && List.Any(f => f.WarehouseCountry == "PL"))
			{
				List.RemoveAll(f => f.WarehouseCountry != "PL");
			}


			#endregion

			#region 收货国家是法国时优先法国仓库(解决运费过高无法发货问题)
			//2022-04-15wjw收货国家是法国的优先从法国仓发货”这一限制取消，改为收货国家是法国的订单（除了CXS的订单之外），按照谁便宜谁发的原则来发货，另外，CXS的订单必须从法国仓发且选择法国本地的物流发
			if (model.temp10 == "FR" && List.Any(f => f.WarehouseCountry == "FR") && order_carrierCdiscount != null)
			{
				var JohnsonStoreDeel = db.scb_WMS_MatchRule.FirstOrDefault(f => f.Type == "JohnsonStoreDeel");

				if (JohnsonStoreDeel != null)
				{
					var JohnsonStoreDianpu = db.scb_WMS_MatchRule_Details.Where(f => f.RID == JohnsonStoreDeel.ID).Select(o => o.Parameter).ToList();
					if (JohnsonStoreDianpu.Any() && JohnsonStoreDianpu.Contains(model.dianpu))
					{
						List.RemoveAll(f => f.WarehouseCountry != "FR");
						if (List.Count == 0)
						{
							message += "CXS的订单必须从法国仓发且选择法国本地的物流发";
							return List;
						}
					}
				}
			}
			#endregion

			#region 排查restock运费超过30欧元的物流方式
			var ALLrestockCKList = "EU97,EU95,EU93,EU91,EU88,EU101,EU71".Split(',').ToList();
			if (List.Any(f => f.Fee >= 30 && ALLrestockCKList.Contains(f.WarehouseName)))
			{
				List.RemoveAll(f => f.Fee >= 30 && ALLrestockCKList.Contains(f.WarehouseName));
				Helper.Info(model.number, "wms_api", $"存在restock超过30欧的物流，进行排除");
				if (List.Count == 0)
				{
					message += $"存在restock超过30欧的物流，不进行匹配";
					return List;
				}
			}
			#endregion

			#region shein平台对发货国的特殊要求 20240116 shein的相关店铺解除所有仓库和物流的限制
			//// 20231009 姚芳芳  GoplusFR-Shein麻烦设置成仅能从法国发货; 

			////IT：06米兰仓发货
			////ES: 德国、法国仓库发货（即EU 01 05 10)
			////SE: 德国、法国仓库发货（即EU 01 05 10)
			////PT: 德国、法国仓库发货（即EU 01 05 10)
			////NL: 德国、法国仓库发货（即EU 01 05 10)
			////IT：EU 07 / 09 / 11仓也可以发货，但仅限于GLS_PL_Milan这个物流
			////FP仓库（EU10），可发物流: GLS_FR_Marl ，DHL_FR_Marl
			////PL仓库（EU07, 09, 11), 可发物流: DHL_berlin, GLS_Berlin
			////DE仓库（EU01, 05) 物流不受限制

			//var canDeFrDianpus = new List<string>() { "GOPLUSDE-SHEIN", "GOPLUSFR-SHEIN", "GoplusSE-Shein", "GoplusPT-Shein", "GoplusNL-Shein", "GoplusES-Shein" };
			////if (Order.dianpu.ToLower() == "goplusfr-shein")
			////{
			////	var cangkus_fr = new List<string>() { "EU10" };
			////	List.RemoveAll(p => !cangkus_fr.Contains(p.WarehouseName));
			////	Helper.Info(Order.number, "wms_api", $"GoplusFR-Shein 仅能从法国发货：{JsonConvert.SerializeObject(List)}");
			////}
			////  20231009 姚芳芳 GoplusPL-Shein麻烦设置成仅能从波兰发货  20231225 并且不能用DHL_BERLIN  GLS_BERLIN发货
			//if (Order.dianpu.ToLower() == "gopluspl-shein")
			//{
			//	var cangkus_pl = new List<string>() { "EU07", "EU09", "EU11" };
			//	List = List.Where(p => cangkus_pl.Contains(p.WarehouseName) && p.SericeType.ToUpper() != "DHL_BERLIN" && p.SericeType.ToUpper() != "GLS_BERLIN").ToList();
			//	message = List.Count == 0 ? "匹配失败，不符合发货规则！" : "";
			//	Helper.Info(Order.number, "wms_api", $"GoplusPL-Shein 仅能从波兰发货，并且不能用DHL_Berlin,GLS_Berlin：{JsonConvert.SerializeObject(List)}");
			//}
			//else if (Order.dianpu.ToLower() == "goplusit-shein")
			//{
			//	var onlyGLS_PL_Milan_cangkus_it = new List<string>() { "EU07", "EU09", "EU11" };
			//	List = List.Where(p => p.WarehouseName == "EU06" || (onlyGLS_PL_Milan_cangkus_it.Contains(p.WarehouseName) && p.SericeType.ToUpper() == "GLS_PL_Milan")).ToList();
			//	Helper.Info(Order.number, "wms_api", $"GoplusIT-Shein 仅能从EU06发货,EU07/09/11仓也可以发货，但仅限于GLS_PL_Milan：{JsonConvert.SerializeObject(List)}");
			//	message = List.Count == 0 ? "匹配失败，不符合发货规则！" : "";
			//}
			////德国、法国发货的店铺 GoplusSE-Shein   GoplusPT-Shein  GoplusNL-Shein  GoplusES-Shein  德国、法国仓库发货（即EU 01 05 10)
			//else if (canDeFrDianpus.Any(p => p.ToLower() == Order.dianpu.ToLower()))
			//{
			//	List = List.Where(o =>
			//	((o.WarehouseName == "EU09" || o.WarehouseName == "EU07" || o.WarehouseName == "EU11") && (o.SericeType.ToUpper() == "DHL_BERLIN" || o.SericeType.ToUpper() == "InterDHL_berlin".ToUpper() || o.SericeType.ToUpper() == "DHLLARGE_BERLIN" || o.SericeType.ToUpper() == "InterDHLlarge_berlin".ToUpper() || o.SericeType.ToUpper() == "GLS_BERLIN")) ||
			//	((o.WarehouseName == "EU01" || o.WarehouseName == "EU05" || o.WarehouseName == "EU10"))).ToList();
			//	Helper.Info(Order.number, "wms_api", $" GoplusSE-Shein,GoplusPT-Shein,GoplusNL-Shein,GoplusES-Shein 能从EU01,EU05,EU10发货 从波兰发货，并且只能用DHL_Berlin,GLS_Berlin：{JsonConvert.SerializeObject(List)}");
			//	message = List.Count == 0 ? "匹配失败，不符合发货规则！" : "";
			//}

			if (model.xspt.ToLower() == "shein")
			{
				//20250217 陈赛芬 西班牙、葡萄牙岛屿单不发
				var sql = $"SELECT postcode FROM scb_postcode_eu_islands where country ='{model.temp10}' and postcode = '{model.zip}' and state = 0";
				if ((model.temp10 == "ES" || model.temp10 == "PT") && db.Database.SqlQuery<string>(sql).Any())
				{
					List.RemoveAll(p => true);
					message = List.Count == 0 ? $"匹配失败，shein，西班牙、葡萄牙岛屿单不发" : "";
					Helper.Info(model.number, "wms_api", $"shein，西班牙、葡萄牙岛屿单不发");
				}
				//shein 物流规则
				if (model.temp10 == "DE")
				{
					List = List.Where(p => p.SericeType != "SDA_PL_IT").ToList();
					Helper.Info(model.number, "wms_api", $"shein德国订单停掉SDA_PL_IT物流，其他正常都可发");
				}
				else
				{
					var ban_ways = "DHLLARGE_BERLIN,interdhl_berlin,DHL_Berlin,GLS_Berlin,DHL_FR_Marl,interDHL_FR_Marl,GLS_FR_Marl,SDA_PL_IT".Split(',').ToList();
					List = List.Where(p => !ban_ways.Any(o => o.ToLower() == p.SericeType.ToLower())).ToList();
					Helper.Info(model.number, "wms_api", $"shein德国外的其他国家订单，停掉DHL_Berlin，GLS_Berlin, DHL_FR_Marl，GLS_FR_Marl，SDA_PL_IT，直发物流正常发");
				}
			}
			//20250306 邵露露西班牙 Mogán 35120不发
			if (model.temp10 == "ES" && model.city == "Mogán" && model.zip == "35120")
			{
				List.RemoveAll(p => true);
				message = List.Count == 0 ? $"匹配失败，西班牙城市:Mogán 邮编:35120 不发" : "";
				Helper.Info(model.number, "wms_api", $"西班牙城市:Mogán 邮编:35120 不发");
			}
			#endregion


			#region Temu 发货限制 姚芳芳

			#region 20240802 新的，根据配置的上架sku限制发货仓和物流
			if (model.xspt.ToLower() == "temu")
			{
				//20250318 姚芳芳 EU12禁发BRT
				if (List.Any(f => f.WarehouseName == "EU12" && f.SericeType == "BRT"))
				{
					List = List.Where(p => !(p.WarehouseName == "EU12" && p.SericeType == "BRT")).ToList();
					Helper.Info(model.number, "wms_api", $"{model.xspt}平台EU12禁止使用BRT发货");
				}
				//20250804 厉彦 komfort开头的店铺物流规则和FDS一样，不只是KomfortDE-TEMU这家
				if (!(model.dianpu.StartsWith("FDS") || model.dianpu.StartsWith("Komfort")))
				{
					var sqlstr = $@"select dtwi.* from erp2..Data_Temu_Inventory_Single_warehouseid  a
left join cxtrade.dbo.Data_Temu_WarehouseId dtwi on a.WarehouseId = dtwi.WarehouseId
where ProductSkuId ='{sheetItem.itemno}' and  StoreName =  '{model.dianpu}'";
					var temu_WarehouseIds = db.Database.SqlQuery<Data_Temu_WarehouseId>(sqlstr).ToList();
					if (!temu_WarehouseIds.Any())
					{
						message += $"未获取到temu店铺上架sku：{sheetItem.itemno}的仓库信息，请联系快乐星球";
						Helper.Info(model.number, "wms_api", message);
						return new List<OrderMatchEntity>();
					}
					var tempList = new List<OrderMatchEntity>();
					var temuMsg = string.Empty;
					foreach (var temu_WarehouseId in temu_WarehouseIds)
					{
						if (temu_WarehouseId.Ways.ToLower().Contains("dhl_berlin"))
						{
							temu_WarehouseId.Ways += ",DHLLARGE_BERLIN,interdhl_berlin";
						}
						if (temu_WarehouseId.Ways.ToLower().Contains("dhl_fr_marl"))
						{
							temu_WarehouseId.Ways += ",interDHL_FR_Marl";
						}
						var ways = temu_WarehouseId.Ways.Split(',').ToList();
						tempList.AddRange(List.Where(p => p.WarehouseName == temu_WarehouseId.CompWareId && ways.Any(o => o.ToLower() == p.SericeType.ToLower())).ToList());
						temuMsg += $",仓库 {temu_WarehouseId.CompWareId} {temu_WarehouseId.WarehouseId},可用物流 {temu_WarehouseId.Ways}";
					}
					if (tempList.Count > 0)
					{
						tempList = tempList.Distinct().ToList();
					}
					Helper.Info(model.number, "wms_api", $"temu上架sku：{sheetItem.itemno}{temuMsg}：{JsonConvert.SerializeObject(List)}");
					if (!tempList.Any())
					{
						message = $"匹配失败，temu上架sku：{sheetItem.itemno}{temuMsg},无库存";
						return new List<OrderMatchEntity>();
					}
					else
					{
						List = tempList;
					}
				}
				else
				{
					var sqlstr = $"select distinct CompWareId,StoreName,ways from Data_Temu_WarehouseId where storename  = '{model.dianpu}'";
					var WarehouseIds = db.Database.SqlQuery<Data_Temu_WarehouseId>(sqlstr).ToList();
					Helper.Info(model.number, "wms_api", $"{model.dianpu},temu配置的发货物流：{JsonConvert.SerializeObject(WarehouseIds)}");
					foreach (var item in WarehouseIds)
					{
						if (item.Ways.ToLower().Contains("dhl_berlin"))
						{
							item.Ways += ",DHLLARGE_BERLIN,interdhl_berlin";
						}
						if (item.Ways.ToLower().Contains("dhl_fr_marl"))
						{
							item.Ways += ",interDHL_FR_Marl";
						}
						var ways = item.Ways.Split(',').ToList();
						var ckinfo = scbcks.FirstOrDefault(p => p.id == item.CompWareId);
						var itemWarehouseNames = scbcks.Where(p => p.id == item.CompWareId || p.RealWarehouse == item.CompWareId).Select(p => p.name).ToList();
						//List.Where(p => p.WarehouseName == item.CompWareId && !ways.Any(o => o.ToLower() == p.SericeType.ToLower())).ToList().ForEach(x => { List.Remove(x); });
						List.Where(p => itemWarehouseNames.Contains(p.WarehouseName) && !ways.Any(o => o.ToLower() == p.SericeType.ToLower())).ToList().ForEach(x => { List.Remove(x); });
					}

				}
			}

			#endregion

			#region 20240429 姚芳芳 TEMU 发货限制   注释
			//var warehouses_de = "EU01,EU05".Split(',').ToList();
			//var warehouses_pl = "EU07,EU09,EU11".Split(',').ToList();
			//var warehouses_fr = "EU10".Split(',').ToList();
			//var warehouses_it = "EU06".Split(',').ToList();

			//if (Order.dianpu == "Temugymax-DE" || Order.dianpu == "Gymaxpt-DE" || Order.dianpu == "Gymaxmaster-DE" || Order.dianpu == "Gymaxflame-DE")
			//{
			//	List = List.Where(p => warehouses_de.Contains(p.WarehouseName)
			//				|| (warehouses_pl.Contains(p.WarehouseName) && (p.SericeType == "DHL_berlin" || p.SericeType.ToUpper() == "DHLLARGE_BERLIN" || p.SericeType.ToLower() == "interdhl_berlin" || p.SericeType == "GLS_Berlin"))
			//		   ).ToList();
			//	Helper.Info(Order.number, "wms_api", $"{Order.dianpu}从DE仓发货，不限制物流，从PL仓发货，只能用DHL_berlin, GLS_Berlin：{JsonConvert.SerializeObject(List)}");
			//	message = List.Count == 0 ? $"匹配失败，{Order.dianpu}从DE仓发货，不限制物流，从PL仓发货，只能用DHL_berlin, GLS_Berlin" : "";
			//}
			//if (Order.dianpu == "Gymaxmaster-FR" || Order.dianpu == "Gymaxpt-FR" || Order.dianpu == "Temugymax-FR")
			//{
			//	List = List.Where(p => p.WarehouseName == "EU10" && p.SericeType != "DHL_FR_Marl" && p.SericeType != "GLS_FR_Marl").ToList();
			//	Helper.Info(Order.number, "wms_api", $"{Order.dianpu}从EU10发货，禁发DHL_FR_Marl, GLS_FR_Marl：{JsonConvert.SerializeObject(List)}");
			//	message = List.Count == 0 ? $"匹配失败，{Order.dianpu}只能从EU10发货,禁发DHL_FR_Marl, GLS_FR_Marl！" : "";
			//}
			//if (Order.dianpu == "Temugymax-IT" || Order.dianpu == "Gymaxpt-IT")
			//{
			//	List = List.Where(p => p.WarehouseName == "EU06").ToList();
			//	Helper.Info(Order.number, "wms_api", $"{Order.dianpu}从EU06发货，发往意大利，可使用意大利仓库的任一物流：{JsonConvert.SerializeObject(List)}");
			//	message = List.Count == 0 ? $"匹配失败，{Order.dianpu}只能从EU06发货！" : "";
			//}
			//if (Order.dianpu == "Gymaxpt-PL" || Order.dianpu == "Temugymax-ES")
			//{
			//	List = List.Where(p => warehouses_pl.Contains(p.WarehouseName) && (p.SericeType == "DHL_berlin" || p.SericeType.ToUpper() == "DHLLARGE_BERLIN" || p.SericeType.ToLower() == "interdhl_berlin" || p.SericeType == "GLS_Berlin")).ToList();
			//	Helper.Info(Order.number, "wms_api", $"{Order.dianpu}从EU07,EU09,EU11发货，只能用DHL_berlin, GLS_Berlin这两个物流：{JsonConvert.SerializeObject(List)}");
			//	message = List.Count == 0 ? $"匹配失败，{Order.dianpu}只能从EU07,EU09,EU11发货,只能用DHL_berlin, GLS_Berlin这两个物流！" : "";
			//}

			////20240625 胡文馨  Gymaxpt-DE店铺  发货时优先匹配德国仓库
			//if (Order.dianpu == "Gymaxpt-DE" && List.Any(p => warehouses_de.Contains(p.WarehouseName)))
			//{
			//	List = List.Where(p => !warehouses_pl.Contains(p.WarehouseName)).ToList();
			//	Helper.Info(Order.number, "wms_api", $"{Order.dianpu}Gymaxpt-DE店铺,发货时优先匹配德国仓库：{JsonConvert.SerializeObject(List)}");
			//}

			#endregion

			#endregion

			#region FDS-aliexpress 匹配发货限制 姚芳芳
			if (model.dianpu == "FDS-aliexpress")
			{
				var _carrier = db.scb_order_carrier.FirstOrDefault(c => c.dianpu == model.dianpu && c.OrderID == model.OrderID && !string.IsNullOrEmpty(c.temp2));
				if (_carrier != null)
				{
					var shipRules = _carrier.temp2.Split(';').ToList();
					foreach (var item in model.sheets)
					{
						var sku = item.sku;
						var rule = shipRules.FirstOrDefault(r => r.Split(':')[0] == item.sku);
						if (!string.IsNullOrEmpty(rule))
						{
							var shipWarehouseCountry = rule.Split(':')[1].Split(',').ToList();
							if (shipWarehouseCountry.Contains("PL"))
							{
								var ways = "GLS,DPD,UPS_PL,AmbroExpress".Split(',').ToList();
								List = List.Where(p => p.WarehouseCountry == "PL" && ways.Any(o => o.ToLower() == p.SericeType.ToLower())).ToList();
							}
							if (shipWarehouseCountry.Contains("DE"))
							{
								var ways_de = "GLS,DPD,UPS_DE,DHL".Split(',').ToList();
								var ways_pl = "DHL_berlin,GLS_Berlin,DHLLARGE_BERLIN,interdhl_berlin".Split(',').ToList();
								var ways_fr = "DHL_FR_Marl,GLS_FR_Marl,interDHL_FR_Marl".Split(',').ToList();
								List = List.Where(p => (p.WarehouseCountry == "PL" && ways_pl.Any(o => o.ToLower() == p.SericeType.ToLower()))
											|| (p.WarehouseCountry == "DE" && ways_de.Any(o => o.ToLower() == p.SericeType.ToLower()))
											|| (p.WarehouseCountry == "FR" && ways_fr.Any(o => o.ToLower() == p.SericeType.ToLower()))
										).ToList();
							}
							if (shipWarehouseCountry.Contains("FR"))
							{
								var ways = "GEODIS,GLS,DPD,ColissimoL".Split(',').ToList();
								List = List.Where(p => p.WarehouseCountry == "FR" && ways.Any(o => o.ToLower() == p.SericeType.ToLower())).ToList();
							}
							if (shipWarehouseCountry.Contains("IT"))
							{
								var ways = "GLS,BRT,DHL_IT".Split(',').ToList();
								List = List.Where(p => p.WarehouseCountry == "IT" && ways.Any(o => o.ToLower() == p.SericeType.ToLower())).ToList();
								////List = List.Where(p => p.WarehouseCountry == "IT").ToList();
								//message = $"匹配失败，后台上品仓库为IT，暂时无可用发货物流。";
								//return new List<OrderMatchEntity>();
							}
							if (List.Count == 0)
							{
								var warehousecounty = "";
								foreach (var house in shipWarehouseCountry)
								{
									warehousecounty += house + " ";
								}
								message = $"sku:{sku} 指定发货仓国家:" + warehousecounty + "无可发仓库货物流";
								return List;
							}
						}
					}
				}
			}

			#endregion


			//// 20241105 张洁楠 德国的订单，麻烦优先从德国仓库发货
			//if (model.temp10 == "DE" && List.Any(p => "EU01,EU05,EU97,EU93".Contains(p.WarehouseName)))
			//{
			//	List = List.Where(p => "EU01,EU05,EU97,EU93".Contains(p.WarehouseName)).ToList();
			//	Helper.Info(model.number, "wms_api", $"目的地 {model.temp10}，德国的订单，优先从德国仓库发货。{JsonConvert.SerializeObject(List)}");
			//}

			#region  temu、shein 不用DHL_IT
			if ("shein".Contains(model.xspt.ToLower()) && List.Count > 0)
			{
				List = List.Where(p => p.SericeType != "DHL_IT").ToList();
				Helper.Info(model.number, "wms_api", $"shein 不用DHL_IT：{JsonConvert.SerializeObject(List)}");
				message = List.Count == 0 ? "匹配失败，shein 不用DHL_IT" : "";
			}
			#endregion

			#region 兰地花 20240607 homasis店铺的KC55737系列餐桌椅不用米兰物流发
			if ("homasisDE-am,homasisES-am".Contains(model.dianpu) && model.sheets.Any(p => "KC55737BN,KC55737GR,KC55737CF".Contains(p.cpbh)) && List.Count > 0)
			{
				List = List.Where(p => p.WarehouseName != "EU06" && p.WarehouseName != "EU88" && p.SericeType != "GLS_PL_Milan").ToList();
				Helper.Info(model.number, "wms_api", $" homasis店铺的KC55737系列餐桌椅不用米兰物流发：{JsonConvert.SerializeObject(List)}");
				message = List.Count == 0 ? "匹配失败， homasis店铺的KC55737系列餐桌椅不要用米兰物流发" : "";
			}
			#endregion

			//restock调整
			if (model.temp10 == "PL") //波兰，（兰地花2022-08-25）收货地是PL直接排除非本地的退货仓-本地的大仓和退货仓优先发，没货的话再发非本地仓库
			{
				//	var restockCKList_PL = "EU91,EU71".Split(',').ToList();
				//	if (List.Any(f => restockCKList_PL.Contains(f.WarehouseName)))
				//	{
				//		foreach (var item in List)
				//		{
				//			if (restockCKList_PL.Contains(item.WarehouseName))
				//			{
				//				item.Fee = 0;
				//			}
				//		}
				//		//List = List.Where(f => restockCKList_PL.Contains(f.WarehouseName)).ToList();
				//		var strs = string.Join(",", List.Select(o => o.WarehouseName));
				//		Helper.Info(model.number, "wms_api", $"波兰优先本地Restock：{strs}");
				//	}
			}
			else
			{
				var restockCKList = "EU97,EU95,EU93,EU91,EU88,EU101,EU71".Split(',').ToList();
				if (List.Any(f => restockCKList.Contains(f.WarehouseName)))
				{
					foreach (var item in List)
					{
						if (restockCKList.Contains(item.WarehouseName))
						{
							item.Fee -= 1000;
						}
					}
					//List = List.Where(f => restockCKList.Contains(f.WarehouseName)).ToList();
					var strs = string.Join(",", List.Select(o => o.WarehouseName));
					Helper.Info(model.number, "wms_api", $"非波兰国家优先Restock：{strs}");
				}
			}

			#region// 20241105 张洁楠 德国的订单，麻烦优先从德国仓库发货
			//if (model.temp10 == "DE" && List.Any(p => "EU01,EU05,EU97,EU93".Contains(p.WarehouseName)))
			//{
			//	List = List.Where(p => "EU01,EU05,EU97,EU93".Contains(p.WarehouseName)).ToList();
			//	Helper.Info(model.number, "wms_api", $"目的地 {model.temp10}，德国的订单，优先从德国仓库发货。{JsonConvert.SerializeObject(List)}");
			//}
			#endregion

			#region //20241106 张洁楠 法国的优先从法国仓库发货，若法国仓库有货且最终匹配到的物流是geodis时，需要重新调整匹配规则，按照所有仓库现有库存谁便宜谁发原则去匹配
			//if (model.temp10 == "FR" && List.Any(f => f.WarehouseCountry == "FR"))
			//{
			//	if (List.Where(p => p.WarehouseCountry == "FR").OrderBy(p => p.Fee).Select(p => p.SericeType).First() != "GEODIS")
			//	{
			//		List = List.Where(p => p.WarehouseCountry == "FR").ToList();
			//		Helper.Info(model.number, "wms_api", $"法国的优先从法国仓库发货：{JsonConvert.SerializeObject(List)}");
			//	}
			//}
			#endregion

			#region  //20250122 张洁楠 对于欧洲订单，排除掉Temu平台订单：如果目的地是波兰的，优先从波兰本地仓按照谁便宜谁发原则发货，并且EU09优先于EU11，本地仓没货的，再从其他仓库按照谁便宜谁发原则发货；
			//20230928 张洁楠 收货国家是波兰的，优先用波兰本地物流发货（本地物流不包括DHL_Berlin、GLS_Berlin、GLS_PL_Milan），没货的话再按照其他仓库谁便宜谁发的
			//var plLogistics = new List<string>() { "hellmann", "ups_pl", "dpd", "gls" };
			//if (model.temp10 == "PL" && List.Any(p => plLogistics.Contains(p.SericeType)))
			//{
			//	List.RemoveAll(p => !plLogistics.Contains(p.SericeType));
			//	Helper.Info(model.number, "wms_api", $"波兰国家优先波兰本地物流发货：{JsonConvert.SerializeObject(List)}");
			//}

			if (model.xspt.ToLower() != "temu" && model.temp10.ToUpper() == "PL" && List.Any(p => p.WarehouseCountry == "PL"))
			{
				if (List.Any(p => p.WarehouseName == "EU09"))
				{
					List = List.Where(p => p.WarehouseName == "EU09").ToList();
				}
				else
				{
					List = List.Where(p => p.WarehouseCountry == "PL").ToList();
				}
				Helper.Info(model.number, "wms_api", $"波兰国家优先波兰本地物流发货，且EU09优先于EU11：{JsonConvert.SerializeObject(List)}");
			}

			#endregion

			#region //20250122 张洁楠 对于欧洲订单，排除掉Temu平台订单：如果目的地是意大利的，优先从意大利本地仓按照谁便宜谁发原则发货，并且EU06优先于EU12，本地仓没货的，再从其他仓库按照谁便宜谁发原则发货；
			//if (model.xspt.ToLower() != "temu" && model.temp10.ToUpper() == "IT" && List.Any(p => p.WarehouseCountry == "IT"))
			//{
			//	if (List.Any(p => p.WarehouseName == "EU06"))
			//	{
			//		List = List.Where(p => p.WarehouseName == "EU06").ToList();
			//	}
			//	else
			//	{
			//		List = List.Where(p => p.WarehouseCountry == "IT").ToList();
			//	}
			//}
			#endregion

			#region   20240724 朴米夏 米兰发的包裹 不要用 SDA_ITInternational 这个物流，投递时间会超过3个星期太长
			var ban_SDA_ITInterRule = db.scb_WMS_MatchRule.FirstOrDefault(f => f.Type == "Ban_SDA_ITInter");
			var ban_SDA_ITInterDetails = db.scb_WMS_MatchRule_Details.Where(p => p.RID == ban_SDA_ITInterRule.ID).ToList();
			if (ban_SDA_ITInterDetails.Any() && List.Any())
			{
				if (model.temp10.ToUpper() != "IT" && List.Where(p => p.Logicswayoid == 117 && p.WarehouseCountry == "IT").Count() > 0 && ban_SDA_ITInterDetails.Any(p => (p.Type == "Store" && p.Parameter.ToLower() == model.dianpu.ToLower()) || (p.Type == "platform" && p.Parameter.ToLower() == model.xspt.ToLower())))
				{
					List = List.Where(p => p.WarehouseCountry != "IT" && p.Logicswayoid != 117).ToList();
					Helper.Info(model.number, "wms_api", $"{model.dianpu} 店铺不用SDA_IT ");
					message = List.Count == 0 ? $"匹配失败，{model.dianpu} 店铺不用SDA_IT " : "";
				}
			}

			#endregion


			#region 欧洲部分货号因尺寸问题需要调整发货物流
			//20250305 张洁楠 指定货号 最便宜的发货物流是UPS_PL时，需要选择第二便宜的物流发货；如果该货物的发货物流选择只有一个UPS_PL时，发货物流为UPS_PL不变
			var specialCpbhs = "HW66368GR,JV11050WH-12,HU10555WT,HU10555CF,JV11729NA,HW68674WH-14F,NP11397GR-2,HW68273WH".Split(',').ToList();
			if (model.sheets.Any(p => specialCpbhs.Contains(p.cpbh)) && List.Any(p => p.SericeType != "UPS_PL"))
			{
				List = List.Where(p => p.SericeType != "UPS_PL").ToList();
				Helper.Info(model.number, "wms_api", $" HW66368GR,JV11050WH-12,HU10555WT,HU10555CF,JV11729NA,HW68674WH-14F,NP11397GR-2,HW68273WH,最便宜的物流是UPS_PL时，选择第二便宜的物流发货：{JsonConvert.SerializeObject(List)}");
			}
			//20250409 张洁楠 TL35154GN，HU10781DK-D 当一开始匹配出来的最便宜的发货物流是UPS_DE时，需要选择第二便宜的物流发货；如果该货物的发货物流选择只有一个UPS_DE时，发货物流为UPS_DE不变
			//20250512 张洁楠 JV11161CF，BE10009WH，HU10741SR - 12，HU10741DK - 12 当一开始匹配出来的最便宜的发货物流是UPS_DE时，需要选择第二便宜的物流发货；如果该货物的发货物流选择只有一个UPS_DE时，发货物流为UPS_DE不变
			//20250613 张洁楠 BE10009WH，TY283250PI，FH10099NY，NP11510 当一开始匹配出来的最便宜的发货物流是UPS_DE时，需要选择第二便宜的物流发货；如果该货物的发货物流选择只有一个UPS_DE时，发货物流为UPS_DE不变
			var specialCpbhs_upsde = "TL35154GN,HU10781DK-D,JV11161CF,BE10009WH,HU10741SR-12,HU10741DK-12,TY283250PI,FH10099NY,NP11510,JV11050WH-12,OP3551DE".Split(',').ToList();
			if (model.sheets.Any(p => specialCpbhs_upsde.Contains(p.cpbh)) && List.Any(p => p.SericeType != "UPS_DE"))
			{
				List = List.Where(p => p.SericeType != "UPS_DE").ToList();
				Helper.Info(model.number, "wms_api", $" TL35154GN,HU10781DK-D,JV11161CF,BE10009WH,HU10741SR-12,HU10741DK-12,TY283250PI,FH10099NY,NP11510,JV11050WH-12,OP3551DE,最便宜的物流是UPS_DE时，选择第二便宜的物流发货：{JsonConvert.SerializeObject(List)}");
			}
			#endregion

			#region  //20250701 张洁楠 发货仓库为法国仓并且发货目的地是法国的，在匹配订单时，需要将法国DPD价格降低2.1欧之后再去匹配，并且周一到周日每天当天的所有法国dpd订单量限制在300单（超过300单就不调价，z）
			//20250617 张洁楠  欧洲订单优先匹配法国DPD,发货仓库为法国仓并且发货目的地是法国的，若第二便宜物流是dpd并且和第一便宜物流运费差距在0.5欧以及之内的订单需要优先选择法国dpd发货；并且这部分优先选择dpd发货的包裹量周一到周日每天限制在60单以内；
			//20250708 张洁楠  将JZ10241该货号排除
			//20250731 张洁楠  dpd从原来的降2.1变成降1.8
			//20250818 张洁楠  这个还是变成降2.1吧，我发现单量还是不够
			if (!model.sheets.Any(p => p.cpbh == "JZ10241") && model.temp10 == "FR" && List.Any(p => p.SericeType == "DPD" && p.WarehouseCountry == "FR"))//法国订单，可以发法国DPD
			{
				//获取最优结果
				var bestMatch = List.OrderBy(p => p.Fee).ThenBy(p => p.logicsPriority).FirstOrDefault();
				//发货仓库为法国，且不是DPD不支持的重量
				if (bestMatch != null && bestMatch.WarehouseCountry == "FR" && bestMatch.SericeType != "DPD")
				{
					var dat_now = AMESTime.AMESNowDE;//DateTime.Now;
					var dat_start = new DateTime(dat_now.Year, dat_now.Month, dat_now.Day, 6, 0, 0);
					var dat_end = dat_start.AddDays(1);
					//单量限制300，不到300，DPD调价匹配
					//20250710 张洁楠 法国dpd限量改到250单吧
					//20250723 张洁楠 法国dpd限量改到280单吧
					//20250911 张洁楠 周六到周一打单加起来不超过880，其他时候还是每天不超过280 不变
					var dpd_countLimit = 280;
					if (dat_now.DayOfWeek == DayOfWeek.Saturday)
					{
						dpd_countLimit = 880;
					}
					else if (dat_now.DayOfWeek == DayOfWeek.Sunday)
					{
						dpd_countLimit = 880;
						dat_start = dat_start.AddDays(-1);
					}
					else if (dat_now.DayOfWeek == DayOfWeek.Monday)
					{
						dpd_countLimit = 880;
						dat_start = dat_start.AddDays(-2);
					}

					if (db.scb_xsjl.Where(p => p.country == "EU" && p.temp10 == "FR" && p.warehouseid == 106 && p.logicswayoid == 19 && p.state == 0 && p.filterflag > 5 && p.fhdate > dat_start && p.fhdate < dat_end).Count() < dpd_countLimit)
					{
						foreach (var item in List)
						{
							if (item.WarehouseCountry == "FR" && item.SericeType == "DPD")
							{
								Helper.Info(model.number, "wms_api", $"发货仓库为法国仓并且发货目的地是法国的，需要将法国DPD价格-2.1欧之后再去匹配，周一到周日每天当天的所有法国dpd订单量限制在250单,原价{item.Fee},现价{item.Fee - 2.1m}");
								item.Fee = item.Fee - 2.1m;
							}
						}
					}
				}
			}
			#endregion



			#region 修正错误dhl服务
			foreach (var item in List)
			{
				if (item.SericeType == "DHL" || item.SericeType == "DHLlarge")
				{
					if (item.WarehouseCountry != model.temp10 && (item.Logicswayoid == 20 || item.Logicswayoid == 23) && item.WarehouseCountry != "PL")
					{
						item.Logicswayoid = 23;
						item.SericeType = "InterDHL";
					}
				}
			}
			#endregion

			//dhl 加价2欧元
			foreach (var item in List)
			{
				if (model.temp10.ToUpper() != "DE" && (item.SericeType.ToUpper() == "INTERDHL" || item.SericeType.ToUpper() == "DHL_BERLIN" || item.SericeType.ToUpper() == "INTERDHL_BERLIN" || item.SericeType.ToUpper() == "DHL_FR_MARL" || item.SericeType.ToUpper() == "INTER_DHL_FR_MARL" || item.SericeType.ToUpper() == "INTERDHL_FR_MARL"))
				{
					Helper.Info(model.number, "wms_api", $"国际件{item.SericeType},{item.WarehouseName},需要涨价2元,原价{item.Fee},现价{item.Fee + 2}");
					item.Fee = item.Fee + 2;
				}
				#region  Colissimo_PL_FR匹配设置   20231024 张洁楠，取消设置
				////20231019 张洁楠 Colissimo_PL_FR这个物流正式开启匹配之后，波兰中转物流（DHL_Berlin、GLS_Berlin、GLS_PL_Milan）的运费在原运费基础上+4欧之后再进行后续的匹配
				//var PLTransferSericeTypeList = new List<string>() { "DHL_BERLIN", "GLS_Berlin", "GLS_Berlin" };
				//if (Order.temp10.ToUpper() == "FR" && item.WarehouseCountry == "PL" && PLTransferSericeTypeList.Any(p => p.ToUpper() == item.SericeType.ToUpper()))
				//{
				//	Helper.Info(Order.number, "wms_api", $"波兰中转物流（DHL_Berlin、GLS_Berlin、GLS_PL_Milan）,{item.WarehouseName},需要涨价4元,原价{item.Fee},现价{item.Fee + 4}");
				//	item.Fee = item.Fee + 4;
				//}
				////20231019 张洁楠 Colissimo_PL_FR这个物流正式开启匹配之后，波兰中转物流（波兰GLS、波兰DPD、Hellmann、UPS_PL）的运费在原运费基础上+2欧之后再进行后续的匹配
				//var PLLocalSericeTypeList = new List<string>() { "GLS", "DPD", "Hellmann", "UPS_PL" };
				//if (Order.temp10.ToUpper() == "FR" && item.WarehouseCountry == "PL" && PLTransferSericeTypeList.Any(p => p.ToUpper() == item.SericeType.ToUpper()))
				//{
				//	Helper.Info(Order.number, "wms_api", $"波兰本地物流（波兰GLS、波兰DPD、Hellmann、UPS_PL）,{item.WarehouseName},需要涨价2元,原价{item.Fee},现价{item.Fee + 2}");
				//	item.Fee = item.Fee + 2;
				//}
				#endregion

			}
			//UPS

			//加价2欧元2022-10-19取消
			//foreach (var item in List)
			//{
			//    if (item.SericeType.ToUpper() == "UPS_PL")
			//    {
			//        Helper.Info(Order.number, "wms_api", $"UPS_PL{item.SericeType},{item.WarehouseName},控制单量，需要涨价500元,原价{item.Fee},现价{item.Fee + 500}");
			//        item.Fee = item.Fee + 500;
			//    }
			//}

			#region 20250307 张洁楠 DE、FR、IT、PL的订单，本地仓有货时，要综合所有有货仓库可发物流按照谁便宜谁发的原则匹配物流及运费，然后拿本地仓的最便宜的物流以及运费去比较，如果本地最便宜物流的运费-综合所有可发物流最便宜的运费>=10，这种时候以综合所有可发物流中最便宜的运费以及对应物流为主
			//20250325 张洁楠 波兰订单的优先从波兰仓库发，没有这个5欧的限制
			//指定的收货地，且存在本地仓
			if ("DE,FR,IT".Contains(model.temp10) && List.Any(p => p.WarehouseCountry == model.temp10))
			{
				var localWarehouseBestMatch = List.Where(p => p.WarehouseCountry == model.temp10).OrderBy(p => p.Fee).First();
				if (model.temp10 == "PL" || model.temp10 == "IT")
				{
					localWarehouseBestMatch = List.Where(p => p.WarehouseCountry == model.temp10).OrderBy(p => p.Fee).ThenBy(p => p.WarehouseName).ThenBy(p => p.logicsPriority).First();
				}
				var warehouseBestMatch = List.OrderBy(p => p.Fee).ThenBy(p => p.logicsPriority).First();
				Helper.Info(model.number, "wms_api", $"目的地：{model.temp10}，本地仓最便宜物流：[{JsonConvert.SerializeObject(localWarehouseBestMatch)}],综合最便宜：[{JsonConvert.SerializeObject(warehouseBestMatch)}]");
				if (localWarehouseBestMatch.Fee - warehouseBestMatch.Fee >= 5)
				{

				}
				else
				{
					List = List.Where(p => p.WarehouseCountry == model.temp10).OrderBy(p => p.Fee).ThenBy(p => p.logicsPriority).ToList();
				}
			}

			#endregion

			return List;
		}

		/// <summary>
		/// 匹配仓库
		/// </summary>
		/// <param name="Order"></param>
		/// <param name="OrderMatch"></param>
		private void MatchWarehouse2(scb_xsjl Order, string datformat, ref OrderMatchResult OrderMatch)
		{
			var db = new cxtradeModel();
			var oid = OrderMatch.Logicswayoid;
			var CurrentLogistics = db.scb_Logisticsmode.Where(f => f.oid == oid).FirstOrDefault();
			var MatchRules = db.scb_WMS_MatchRule.Where(f => f.State == 1).ToList();

			var Tocountry = Order.temp10;
			if (string.IsNullOrEmpty(datformat))
				datformat = DateTime.Now.ToString("yyyy-MM-dd");

			#region 邮编校验+偏远邮编校验
			if (!"WalmartVendor,wayfair,macys,overstock-supplier,pioneer002,Costway-philip".Split(',').Contains(Order.dianpu)
						&& Tocountry == "US")
			{
				var statsa = string.Empty;
				var zips = db.scb_ups_zip.FirstOrDefault(z => Order.zip.StartsWith(z.DestZip));
				if (zips != null)
				{
					if ("AK,HI,PR".Split(',').Contains(zips.State))
					{
						OrderMatch.Result.Message = string.Format("匹配错误,州[{0}]不允许匹配！订单编号:{1}", Order.statsa, Order.number);
						return;
					}
				}
				else
				{
					OrderMatch.Result.Message = string.Format("匹配错误,邮编[{0}]无效！订单编号:{1}", Order.zip, Order.number);
					return;
				}
			}
			#endregion

			#region openbox仓库
			if (!string.IsNullOrEmpty(Order.chck))
			{
				if (Order.chck.ToLower() == "openbox")
				{
					OrderMatch.WarehouseName = "US95";
					return;
				}
			}
			#endregion

			#region 匹配仓库
			lock (MatchWarehouseLock)
			{
				#region 区域和仓库区分

				#region 仓库清单
				var WarehouseIDList = new List<string>();
				var WarehouseList = new List<scbck>();

				var cklist = db.scbck.Where(c => c.countryid == "01" && c.state == 1 && c.IsMatching == 1 && c.Origin != "").ToList();
				if (!string.IsNullOrEmpty(Order.zip))
				{
					var zips = db.scb_ups_zip.Where(z => Order.zip.StartsWith(z.DestZip)).ToList();

					if (zips != null)
					{
						zips = zips.OrderBy(z => z.GNDZone).ThenBy(z => z.TNTDAYS).ToList();
						foreach (var item in zips)
						{
							var Origin = item.Origin;
							var ct = cklist.Where(c => c.Origin == Origin).OrderBy(c => c.OriginSort);
							foreach (var item2 in ct)
							{
								WarehouseList.Add(item2);
								WarehouseIDList.Add(item2.id);
							}
						}
					}
				}

				if (OrderMatch.SericeType == "UPSInternational")
				{
					WarehouseList = cklist.Where(f => f.id != "US98").ToList();
				}

				var lcks = new List<scbck>();
				//清仓
				var firstsend = cklist.FirstOrDefault(c => c.id.StartsWith("US") && c.firstsend == 2);
				if (firstsend != null && Order.temp10 == "US")
					lcks.Add(firstsend);

				if (WarehouseList.Any())
				{
					if (OrderMatch.SericeType == "UPSInternational")
					{
						var CA_Fedex = MatchRules.FirstOrDefault(f => f.Type == "CA_Fedex");
						var CA_Fedexs = new List<scb_WMS_MatchRule_Details>();
						if (CA_Fedex != null)
						{
							CA_Fedexs = db.scb_WMS_MatchRule_Details.Where(f => f.RID == CA_Fedex.ID && f.Type == "Warehouse").ToList();
							WarehouseList.Where(f => CA_Fedexs.Any(g => g.Parameter == f.id)).ToList().ForEach(x => { WarehouseList.Remove(x); });
						}

						if (Order.temp10 == "CA")
						{
							lcks.Add(db.scbck.First(c => c.id == "US70"));
							lcks.Add(db.scbck.First(c => c.id == "US121"));
							lcks.Add(db.scbck.First(c => c.id == "US20"));
							lcks.Add(db.scbck.First(c => c.id == "US21"));
							if (CA_Fedexs.Any())
							{
								var t = cklist.Where(c => CA_Fedexs.Any(g => g.Parameter == c.id)).ToList();
								if (t.Any())
									lcks.AddRange(t);
							}

						}
					}
					//west加入restock仓库匹配
					if (WarehouseList.First().AreaLocation == "west")
					{
						lcks.Add(db.scbck.First(c => c.id == "US98"));
					}
					//else
					//    lcks = WarehouseList;

					lcks.AddRange(WarehouseList);
				}
				else
					lcks.AddRange(WarehouseList);

				if (OrderMatch.SericeType == null)
					OrderMatch.SericeType = string.Empty;

				#endregion

				#region 分配
				var sheets = db.scb_xsjlsheet.Where(f => f.father == Order.number).ToList();
				var area = new List<int>();
				var ws = new Dictionary<decimal, List<scbck>>();
				foreach (var item in sheets)
				{
					var CurrentItemNo = item.cpbh;
					var sql = string.Format(@"select Warehouse from (
select Warehouse, SUM(sl) kcsl,
isnull((select SUM(sl) tempkcsl from scb_tempkcsl where nowdate = '{2}' and ck = Warehouse and cpbh = '{0}'), 0) tempkcsl
from scb_kcjl_location a inner join scb_kcjl_area_cangw b
on a.Warehouse = b.Location and a.Location = b.cangw
where b.state = 0 and a.Disable = 0  and b.cpbh = '{0}' and '{3}' like '%' + Warehouse + '%'
group by Warehouse) t where kcsl - tempkcsl >= {1}", CurrentItemNo, item.sl, datformat, CurrentLogistics.warehoues);
					var list = db.Database.SqlQuery<string>(sql).ToList();

					ws.Add(item.number, lcks.Where(f => list.Any(g => g == f.id)).ToList());

				}
				if (ws.Any())
				{
					if (ws.Count() == 1)
					{
						var CurrentWarehouse = ws.FirstOrDefault().Value.FirstOrDefault();
						if (CurrentWarehouse == null)
						{
							OrderMatch.Result.Message = "匹配错误,库存不足";
						}
						else
						{
							OrderMatch.WarehouseID = CurrentWarehouse.number;
							OrderMatch.WarehouseName = CurrentWarehouse.id;
						}

					}
					else
					{
						var Baselist = ws.FirstOrDefault().Value;
						foreach (var item in ws)
						{
							Baselist = Baselist.Where(f => item.Value.Any(g => g.id == f.id)).ToList();
						}
						if (Baselist.Any())
						{
							var CurrentWarehouse = Baselist.FirstOrDefault();
							OrderMatch.WarehouseID = CurrentWarehouse.number;
							OrderMatch.WarehouseName = CurrentWarehouse.id;
						}
						else
							OrderMatch.Result.Message = "错误,N个不同的仓库，需要分拆订单";
					}

				}
				else
					OrderMatch.Result.Message = "匹配错误,库存不足";

				#endregion

				#endregion


			}
			#endregion
		}
		#endregion

		#region common
		/// <summary>
		/// 
		/// </summary>
		/// <param name="dianpu">店铺</param>
		/// <param name="xspt">平台</param>
		/// <returns></returns>
		private bool IsAMZNXsptOrDianpu(List<scb_WMS_MatchRule> MatchRules, string xspt, string dianpu, string email)
		{
			var db = new cxtradeModel();
			var MatchRule = MatchRules.First(p => p.Type == "Allow_AMZN");
			var MatchRule_details = db.scb_WMS_MatchRule_Details.Where(p => p.RID == MatchRule.ID).ToList();


			var allowXsptList = MatchRule_details.Where(p => p.Type == "platform").Select(p => p.Parameter).ToList();//"Shein,Sears,TikTok,AM,temu,Groupon".Split(',').ToList();//,Groupon
			var allowDianpuList = MatchRule_details.Where(p => p.Type == "Store").Select(p => p.Parameter).ToList();//"FDSUS".Split(',').ToList();

			var AMZN_FDSUSBP_SendMails = "support@visionupllc.com,rajputnitik222@gmail.com,theusaretails@gmail.com,cerezojandy@gmail.com,knelsen.peter@yahoo.com,ricardo.rivera@borikensm.com,myacrepairnc@gmail.com,jj@trianglelawngames.com".Split(',').ToList();
			var AMZN_Dropshop_SendMails = "hello@ankmanllc.com,cerezojandy@gmail.com".Split(',').ToList();

			if (allowXsptList.Any(p => p.ToLower() == xspt.ToLower())
				|| (allowDianpuList.Any(p => p.ToLower() == dianpu.ToLower()))
				) //客户邮箱
			{
				//虞露莎  20240613	powerstone和Hoocool 这2个店铺帮忙排除Amazonshipping吧，他们店比较小，问题订单影响比较大
				//虞露莎 20240710 powerstone和tomanor ，这两家可以用回Amazonshipping了
				//if ((xspt == "AMAZON" || xspt == "AM") && (dianpu.ToLower() == "powerstone" || dianpu.ToLower() == "hoocool"))
				//{
				//	return false;
				//}
				//else
				//{
				//	return true;
				//}
				//&& ((dianpu == "FDSUS-BP" && AMZN_FDSUSBP_SendMails.Any(p => p == email)) || (dianpu == "Dropshop-HK" && AMZN_Dropshop_SendMails.Any(p => p == email)))
				if (dianpu == "FDSUS-BP" && !AMZN_FDSUSBP_SendMails.Any(p => p == email))
				{
					return false;
				}
				if (dianpu == "Dropshop-HK" && !AMZN_Dropshop_SendMails.Any(p => p == email))
				{
					return false;
				}

				//20240801 武金凤 Tiktok-Deal   Tiktok-onlineshop   Tiktok-Costzon三家店设置不发amazon shipping吧
				var tiktokLimitDianpus = "Tiktok-Deal,Tiktok-onlineshop,Tiktok-Costzon".Split(',').ToList();
				if (tiktokLimitDianpus.Any(p => p.ToLower() == dianpu.ToLower()))
				{
					return false;
				}
				return true;
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="dianpu">店铺</param>
		/// <param name="xspt">平台</param>
		/// <returns></returns>
		private bool IsAMZNXCpbh(string dianpu, List<MatchDetailModel> sheets, List<scb_WMS_MatchRule> MatchRules)
		{
			var canCpbhSendAMZN = true;
			var benInfo = MatchRules.FirstOrDefault(f => f.Type == "Ban_SendAMZNByShop" && f.State == 1);
			if (benInfo != null)
			{
				var benShops = benInfo.Shop.Split(',').ToList();
				var benItemNos = benInfo.ItemNo.Split(',').ToList();
				if ((benShops.Any(p => p == dianpu) && sheets.Any(p => benItemNos.Contains(p.cpbh))) || (sheets.Any(p => (p.sku == "FP10293US-12WH+FP10293US-22WH" || p.sku == "FP10302US-12WH+FP10302US-22WH") && dianpu.ToLower() == "costway")))
				{
					canCpbhSendAMZN = false;
				}
			}
			//史倩文  zimbala ES10267BK 不发amzn
			if (dianpu.ToLower() == "zimbala" && sheets.Any(p => p.cpbh == "ES10267BK"))
			{
				canCpbhSendAMZN = false;
			}
			return canCpbhSendAMZN;
		}

		private bool IsAbnorma(string orderId)
		{
			var db = new cxtradeModel();
			return db.scb_WMS_AbnormalOrder.Any(p => p.OrderID == orderId && p.IsVaild == 1);
		}


		private bool MatchActualWarehouseUS(List<LogisticsWareRelationModel> logisticsmodes, List<string> _UPSSkuList, List<CpbhSizeInfo> cpRealSizeinfos, scb_xsjl order, List<scb_WMS_LogisticsForWeight> configs, List<scb_WMS_MatchRule> MatchRules,
			List<scb_xsjlsheet> sheets, double weightReal, double weight, double weight_g, double? volNum, double? volNum2, double weight_vol, double weight_phy, double? maxNum, double? secNum, string Tocountry,
			bool CanCAToUS, ref OrderMatchResult OrderMatch, ref List<ScbckModel> EffectiveWarehouse, bool HasWMSStock = false, string country = "US", bool ignoreAMZN = false, string aircondition_box = "")
		{
			var db = new cxtradeModel();
			var IsFind2 = false;

			//分体式空调，目前只有US16
			if (!string.IsNullOrEmpty(aircondition_box))
			{
				EffectiveWarehouse = EffectiveWarehouse.Where(p => p.id == "US16" || p.id == "US06").ToList();
				if (!EffectiveWarehouse.Any())
				{
					OrderMatch.Result.Message += "匹配失败，分体式空调配件合并发货仓US16、US06 无库存，建议拆包；";
					return IsFind2;
				}
			}

			#region AMZN优先US16 
			//20240304 武金凤 开honeyjoy店铺和US06 Fontana仓库，两个仓库平均都是500单左右上限
			//20240305 虞露莎 fontana开启时间待定 
			//20240401 武金凤 开US09
			//20240410 武金凤 用打单尺寸和重量
			//20240411 武金凤 美国时间4.16号 us17和us08 发出，所以前一天截单后开US17,US08
			//20240514 史倩文 CA2US优先级比AMZN高哦
			//20240604 武金凤 babyjoy用 打单计费重
			var cklistForAMZN = logisticsmodes.First(p => p.logisticsName == "AMZN").warehouseIds;
			var sheetinfo = sheets.Select(p => new MatchDetailModel { cpbh = p.cpbh, sku = p.sku }).ToList();
			if (!ignoreAMZN && EffectiveWarehouse.Any(p => cklistForAMZN.Contains(p.id)) && !order.dianpu.Contains("Prime")
				&& !(CanCAToUS && Tocountry == "US" && (EffectiveWarehouse.First().id == "US21" || EffectiveWarehouse.First().id == "US121"))
				&& IsAMZNXCpbh(order.dianpu, sheetinfo, MatchRules)
				&& IsAMZNXsptOrDianpu(MatchRules, order.xspt, order.dianpu, order.email))
			{
				#region  符合条件包裹优先发Amz shipping  现阶段只发US16Redlands
				var WarehouseList = EffectiveWarehouse.Where(p => cklistForAMZN.Contains(p.id));
				if (order.temp10 == "US" && ((sheets.Sum(p => p.sl) == 1 && string.IsNullOrEmpty(aircondition_box)) || !string.IsNullOrEmpty(aircondition_box)))
				{
					var cpbh = string.IsNullOrEmpty(aircondition_box) ? sheets.First().cpbh : aircondition_box;
					var girth = 0m;
					var length = 0m;
					var wight = 0m;
					var height = 0m;
					var applyWeightForAMZN = weight;
					var realWeightForAMZN = weightReal;

					var logisticsApply_Size = db.Scb_LogisticsApply_Size.FirstOrDefault(p => p.Country == country && p.LogisticsName == "AMZN" && p.Cpbh == cpbh);
					if (logisticsApply_Size != null)
					{
						length = logisticsApply_Size.Length;
						wight = logisticsApply_Size.Width;
						height = logisticsApply_Size.Heigth;
						applyWeightForAMZN = (double)logisticsApply_Size.Weight;
						realWeightForAMZN = (double)logisticsApply_Size.Weight;
					}
					else
					{
						var good = db.N_goods.FirstOrDefault(p => p.cpbh == cpbh && p.country == "US");
						length = (decimal)good.bzcd;
						wight = (decimal)good.bzkd;
						height = (decimal)good.bzgd;
						var vol_weight = (double)volNum2 / 200;
						applyWeightForAMZN = vol_weight > weight ? vol_weight : weight;
						#region  计算真实计费重
						//var realsize = db.scb_realsize.FirstOrDefault(p => p.cpbh == cpbh && p.country == "US");
						//if (realsize != null)
						//{
						//	var vol_weightReal = (double)realsize.bzcd * (double)realsize.bzkd * (double)realsize.bzgd / 200;//体积重
						//	realWeightForAMZN = vol_weightReal > weightReal ? vol_weightReal : weightReal;
						//}
						//else
						//{
						//	realWeightForAMZN = applyWeightForAMZN;
						//}
						#endregion
					}

					girth = length + (wight + height) * 2;
					if (Math.Ceiling(girth) > 0 && Math.Ceiling(length) <= 47 && Math.Ceiling(wight) < 30 && Math.Ceiling(girth) <= 105 && Math.Ceiling(applyWeightForAMZN) <= 50 && Math.Ceiling(applyWeightForAMZN) > 1)
					{
						scbck ck = null;
						foreach (var item in WarehouseList)
						{
							ck = db.scbck.FirstOrDefault(p => p.id == item.id);
							var upszip = _cacheZip.FirstOrDefault(p => p.Origin == ck.Origin && order.zip.StartsWith(p.DestZip));
							if (ck != null && upszip != null
								 && ((Math.Ceiling(applyWeightForAMZN) > 5 && Math.Ceiling(applyWeightForAMZN) <= 10 && upszip.GNDZone <= 8) || (Math.Ceiling(applyWeightForAMZN) > 10 && Math.Ceiling(applyWeightForAMZN) <= 50 && upszip.GNDZone <= 6)
								|| (Math.Ceiling(applyWeightForAMZN) > 1 && Math.Ceiling(applyWeightForAMZN) <= 5 && upszip.GNDZone <= 8)))
							{
								OrderMatch.Logicswayoid = 114;
								OrderMatch.SericeType = "AMZN";
								IsFind2 = true;
								OrderMatch.WarehouseID = ck.number;
								OrderMatch.WarehouseName = ck.id;
								return IsFind2;
							}
						}
					}
				}

				#endregion
			}
			#endregion

			foreach (var item in EffectiveWarehouse)
			{
				WarehouseLevel(item, _UPSSkuList, configs, MatchRules, order, sheets,
				weight, weight_g, volNum, volNum2, weight_vol, weight_phy, maxNum, secNum, Tocountry, CanCAToUS, ref OrderMatch, false, aircondition_box: aircondition_box);

				if (item.oids.Contains("," + OrderMatch.Logicswayoid + ","))
				{
					if (!HasWMSStock && item.EffectiveInventory < 3)
					{
						var nid = (decimal)sheets.First().father;
						var lowStockReq = new LowStockRequest()
						{
							ItemNumber = sheets.First().cpbh,
							WarehouseIds = EffectiveWarehouse.Select(p => p.id).Distinct().ToList()
						};
						//获取wms库存
						var api = new HttpApi();
						var lowStockRes = api.GetLowStock(country, lowStockReq);
						Helper.Info(nid, "wms_api", $"WMS有效实时库存: {JsonConvert.SerializeObject(lowStockRes)}");
						//回刷EffectiveWarehouse
						foreach (var warehouse in EffectiveWarehouse)
						{
							var wmsStock = lowStockRes.WareStockDtos.FirstOrDefault(p => p.WarehouseId == warehouse.id);
							warehouse.EffectiveInventory = wmsStock != null ? wmsStock.AvailableQty : 0;
						}
						HasWMSStock = true;
						return MatchActualWarehouseUS(logisticsmodes, _UPSSkuList, cpRealSizeinfos, order, configs, MatchRules, sheets, weightReal, weight, weight_g, volNum, volNum2, weight_vol, weight_phy, maxNum, secNum, Tocountry, CanCAToUS, ref OrderMatch, ref EffectiveWarehouse, HasWMSStock, ignoreAMZN: ignoreAMZN, aircondition_box: aircondition_box);
					}
					IsFind2 = true;
					OrderMatch.WarehouseID = item.number;
					OrderMatch.WarehouseName = item.id;
					//break;
				}

				if (IsFind2)
				{
					var Fedex3rd_warehouses = logisticsmodes.First(p => p.logisticsName == "FedEx-3rd").warehouseIds;
					var cpRealSizeinfo = cpRealSizeinfos.First();
					var girth = cpRealSizeinfo.bzcd + 2 * (cpRealSizeinfo.bzkd + cpRealSizeinfo.bzgd);
					if (Tocountry == "US" && order.zsl == 1
						&& (cpRealSizeinfo.bzcd > 96 || girth > 130) && cpRealSizeinfo.bzcd < 108 && girth <= 165 && cpRealSizeinfo.weight < 150
						&& (OrderMatch.SericeType.Contains("FEDEX") && !_ThirdBillAccountList.Any(p => p.Logistics == "Fedex" && p.CountryCode == "US" && p.Shop == order.dianpu))
						&& EffectiveWarehouse.Any(p => Fedex3rd_warehouses.Contains(p.id))
						)
					{
						//US12 打单尺寸在130以下不发
						//20240626 张洁楠 US09 只能发FedExOVERSIZE
						if ((item.id == "US12" || item.id == "US09") && volNum <= 130)
						{
							break;
						}
						else if (Fedex3rd_warehouses.Contains(item.id))
						{
							OrderMatch.SericeType = "FedEx-3rd";
							OrderMatch.Logicswayoid = 121;
							break;
						}
						else
						{
							OrderMatch.SericeType = "";
							OrderMatch.WarehouseID = 0;
							OrderMatch.WarehouseName = "";
						}
					}
					else
					{
						break;
					}
				}
			}
			return IsFind2;
		}

		private void MatchActualWarehouseEU(List<scb_xsjlsheet> sheets, List<OrderMatchEntity> matchCKList, ref OrderMatchResult OrderMatch, bool HasWMSStock = false, string country = "EU")
		{
			if (matchCKList.Any())
			{
				var CurrentWarehouse = matchCKList.OrderBy(f => f.Fee).ThenByDescending(f => f.islocalWare).ThenByDescending(f => f.WarehouseID).FirstOrDefault();
				if (CurrentWarehouse != null)
				{
					//库存不足，需要问下wms
					if (!HasWMSStock && CurrentWarehouse.kcsl - CurrentWarehouse.unshipped - CurrentWarehouse.unshipped2 < 3)
					{
						var cpbhlist = sheets.Select(p => p.cpbh).Distinct().ToList();
						var nid = (decimal)sheets.First().father;
						foreach (var cpbh in cpbhlist)
						{
							var lowStockReq = new LowStockRequest()
							{
								ItemNumber = sheets.First().cpbh,
								WarehouseIds = matchCKList.Select(p => p.WarehouseName).Distinct().ToList()
							};
							//获取wms库存
							var api = new HttpApi();
							var lowStockRes = api.GetLowStock(country, lowStockReq);
							//回刷EffectiveWarehouse\
							var needSl = sheets.Where(p => p.cpbh == cpbh).Sum(p => p.sl);
							foreach (var wmsStock in lowStockRes.WareStockDtos)
							{
								if (wmsStock.AvailableQty - needSl < 0)
								{
									matchCKList = matchCKList.Where(p => p.WarehouseName != wmsStock.WarehouseId).ToList();
									Helper.Info(nid, "wms_api", $"WMS有效实时库存={wmsStock.AvailableQty} 不足");
								}
							}
						}
						HasWMSStock = true;
						MatchActualWarehouseEU(sheets, matchCKList, ref OrderMatch, HasWMSStock);
						return;
					}
					OrderMatch.WarehouseID = CurrentWarehouse.WarehouseID;
					OrderMatch.WarehouseName = CurrentWarehouse.WarehouseName;
					OrderMatch.Logicswayoid = CurrentWarehouse.Logicswayoid;
					OrderMatch.SericeType = CurrentWarehouse.SericeType;
					OrderMatch.MatchFee = CurrentWarehouse.OldFee;
					return;
				}
			}
			OrderMatch.Result.Message = string.IsNullOrEmpty(OrderMatch.Result.Message) ? "匹配错误,库存不足" : OrderMatch.Result.Message;
			return;
		}

		private void MatchActualWarehouseEUV3(List<EUMatchSheetModel> sheets, List<OrderMatchEntity> matchCKList, ref OrderMatchResult OrderMatch, bool HasWMSStock = false, string country = "EU")
		{
			if (matchCKList.Any())
			{
				var CurrentWarehouse = matchCKList.OrderBy(f => f.Fee).ThenByDescending(f => f.islocalWare).ThenByDescending(f => f.WarehouseID).FirstOrDefault();
				if (CurrentWarehouse != null)
				{
					//库存不足，需要问下wms
					if (!HasWMSStock && CurrentWarehouse.kcsl - CurrentWarehouse.unshipped - CurrentWarehouse.unshipped2 < 3)
					{
						var cpbhlist = sheets.Select(p => p.cpbh).Distinct().ToList();
						var nid = (decimal)sheets.First().father;
						foreach (var cpbh in cpbhlist)
						{
							var lowStockReq = new LowStockRequest()
							{
								ItemNumber = sheets.First().cpbh,
								WarehouseIds = matchCKList.Select(p => p.WarehouseName).Distinct().ToList()
							};
							//获取wms库存
							var api = new HttpApi();
							var lowStockRes = api.GetLowStock(country, lowStockReq);
							//回刷EffectiveWarehouse\
							var needSl = sheets.Where(p => p.cpbh == cpbh).Sum(p => p.sl);
							foreach (var wmsStock in lowStockRes.WareStockDtos)
							{
								if (wmsStock.AvailableQty - needSl < 0)
								{
									matchCKList = matchCKList.Where(p => p.WarehouseName != wmsStock.WarehouseId).ToList();
									Helper.Info(nid, "wms_api", $"WMS有效实时库存={wmsStock.AvailableQty} 不足");
								}
							}
						}
						HasWMSStock = true;
						MatchActualWarehouseEUV3(sheets, matchCKList, ref OrderMatch, HasWMSStock);
						return;
					}
					if ((bool)CurrentWarehouse.isNeedSplit)
					{
						OrderMatch.Result.Message = $"分开发货更便宜，建议拆包！匹配仓库：{CurrentWarehouse.WarehouseName}，物流服务：{CurrentWarehouse.SericeType}，单包运费：{CurrentWarehouse.OldFee}，多包运费：{CurrentWarehouse.Fee_Box2}(boxfee_2),{CurrentWarehouse.Fee_Box3}(boxfee_3)";
					}
					OrderMatch.WarehouseID = CurrentWarehouse.WarehouseID;
					OrderMatch.WarehouseName = CurrentWarehouse.WarehouseName;
					OrderMatch.Logicswayoid = CurrentWarehouse.Logicswayoid;
					OrderMatch.SericeType = CurrentWarehouse.SericeType;
					if (sheets.Sum(p => p.sl) == 3)
					{
						OrderMatch.MatchFee = CurrentWarehouse.Fee_Box3;
					}
					else if (sheets.Sum(p => p.sl) == 2)
					{
						OrderMatch.MatchFee = CurrentWarehouse.Fee_Box2;
					}
					else
					{
						OrderMatch.MatchFee = CurrentWarehouse.OldFee;
					}
					return;
				}
			}
			OrderMatch.Result.Message = string.IsNullOrEmpty(OrderMatch.Result.Message) ? "匹配错误,库存不足" : OrderMatch.Result.Message;
			return;
		}

		private bool MatchActualWarehouseUSV3(List<scbck> restockck, List<string> _UPSSkuList, List<LogisticsWareRelationModel> logisticsmodes, MatchModel order, List<scb_WMS_LogisticsForWeight> configs, List<scb_WMS_MatchRule> MatchRules, List<CpbhSizeInfo> cpRealSizeinfos, List<CpbhSizeInfo> cpExpressSizeinfos,
		double weightReal, double weight, double weight_g, double? volNum, double? volNum2, double? weight_vol, string Tocountry, bool CanCAToUS, ref OrderMatchResult OrderMatch, ref List<ScbckModel> EffectiveWarehouse, string aircondition_box = "")
		{
			var db = new cxtradeModel();
			var IsFind2 = false;
			if (!EffectiveWarehouse.Any())
			{
				return false;
			}
			//分体式空调，目前只有US16
			if (!string.IsNullOrEmpty(aircondition_box))
			{
				EffectiveWarehouse = EffectiveWarehouse.Where(p => p.id == "US16" || p.id == "US06").ToList();
				if (!EffectiveWarehouse.Any())
				{
					OrderMatch.Result.Message += "匹配失败，分体式空调配件合并发货仓US16、US06 无库存，建议拆包；";
					return IsFind2;
				}
			}

			//国际件
			if (order.toCountry == "CA")
			{
				var warehouse = EffectiveWarehouse.FirstOrDefault();
				if (warehouse != null)
				{
					//加拿大本地件,计费重在40lbs以下的都给UPS，其余给FedEx
					if (warehouse.id == "US21" || warehouse.id == "US121")
					{
						var Warehouse = EffectiveWarehouse.Where(p => p.id == "US21" || p.id == "US121").First();
						var ups_weight = volNum2 / 320;
						//计费重
						double jfweight = weight;
						bool is_volweight = false;
						if (ups_weight > weight)
						{
							jfweight = (double)ups_weight;
							is_volweight = true;
						}
						var cpRealSizeinfo = cpRealSizeinfos.First();
						var girth = cpRealSizeinfo.bzcd + 2 * (cpRealSizeinfo.bzgd + cpRealSizeinfo.bzkd);
						var config = configs.FirstOrDefault(f => f.Warehouses.Contains(Warehouse.id));
						if (config == null)
							config = configs.FirstOrDefault(f => f.Sort == 1);
						//if ((jfweight) > double.Parse(config.Weight) || girth > 130 || cpRealSizeinfo.bzcd > 96)
						if (girth > 130 || cpRealSizeinfo.bzcd > 96
							|| (is_volweight && ups_weight > 63) || (!is_volweight && weight > 90)
							)
						{
							OrderMatch.Logicswayoid = 18;
							OrderMatch.SericeType = GetUSServceType(OrderMatch.Logicswayoid);
						}
						else
						{
							OrderMatch.Logicswayoid = 4;
							OrderMatch.SericeType = GetUSServceType(OrderMatch.Logicswayoid);
						}
						//IsFind2 = true;
					}
					else
					{
						OrderMatch.Logicswayoid = 48;
						OrderMatch.SericeType = GetUSServceType(OrderMatch.Logicswayoid);
					}

					if (OrderMatch.Logicswayoid > 0 && warehouse.oids.Contains("," + OrderMatch.Logicswayoid + ","))
					{
						OrderMatch.WarehouseID = warehouse.number;
						OrderMatch.WarehouseName = warehouse.id;
						IsFind2 = true;
					}
				}
			}
			else if (order.toCountry == "MX")
			{
				var warehouse = EffectiveWarehouse.FirstOrDefault();
				if (warehouse != null)
				{
					OrderMatch.Logicswayoid = 48;
					OrderMatch.SericeType = GetUSServceType(48);
					OrderMatch.WarehouseID = warehouse.number;
					OrderMatch.WarehouseName = warehouse.id;
					IsFind2 = true;
				}
			}
			else if (order.toCountry == "US")//美国本地件
			{
				#region usps
				var smartposts = MatchRules.Where(f => f.Type == "Allow_SmartPost" && f.State == 1).ToList();

				//美国无库存ca可发
				if (CanCAToUS && EffectiveWarehouse.Count() == 1 && EffectiveWarehouse.Any(p => p.id == "US21" || p.id == "US121"))
				{
					OrderMatch.WarehouseID = EffectiveWarehouse.First().number;
					OrderMatch.WarehouseName = EffectiveWarehouse.First().id;
					OrderMatch.Logicswayoid = 79;
					OrderMatch.SericeType = "FedexToUS";
					return true;
				}
				//var ups_weight = volNum2 / 300; //目前用不到了
				//20241021 bestbuyUS不支持发USPS
				//20250120 武金凤 小于等于15盎司都发usps
				//20250918 武金凤 15oz改成12oz,剩下的去匹配哪个便宜发哪个
				//if (weight < 1 && weight_g <= 453 && order.dianpu != "BestbuyUS")
				//20251016阮煜茜  tiktok不发usps
				var weight_oz = weight_g / 1000 * 35.27396192;
				bool canUSPS = true;
				if (weight_oz > 15 || order.dianpu == "BestbuyUS" || (order.xspt == "TikTok" && order.dianpu != "TikTok-Samples"))
				{
					canUSPS = false;
				}
				if (weight_oz <= 12 && canUSPS)
				{
					OrderMatch.WarehouseID = EffectiveWarehouse.First().number;
					OrderMatch.WarehouseName = EffectiveWarehouse.First().id;
					OrderMatch.Logicswayoid = 3;
					OrderMatch.SericeType = "First|Default|Parcel|OFF";
					return true;
				}
				#endregion


				//if (!CanCAToUS)
				//{
				//	EffectiveWarehouse = EffectiveWarehouse.Where(p => p.id != "US21" && p.id != "US121").ToList();
				//}

				//根据cpbh和zone获取物流费用
				var cpbhs = order.details.Select(p => p.cpbh).ToList();
				var zones = EffectiveWarehouse.Where(p => p.GNDZone.HasValue).Select(p => (double)p.GNDZone).ToList();
				var cpbh = order.details.First().cpbh;
				var logisticsmode_FeeList = db.scb_logisticsForproduct_us_new.Where(p => p.IsChecked == 1 && p.goods == cpbh && zones.Contains((double)p.zone)).OrderBy(p => p.fee).ToList(); //&& p.LogisticsType != "FedEx-3rd"

				if (!logisticsmode_FeeList.Any())
				{
					OrderMatch.Result.Message = "匹配失败，未获取到物流运费,请联系OS";
					return false;
				}

				//判断是否oversize
				var fedex3rd_warehouses = logisticsmodes.Where(p => p.logisticsName.StartsWith("FedEx-3rd")).ToList();
				var cpRealSizeinfo = cpRealSizeinfos.First();
				var girth = cpRealSizeinfo.bzcd + 2 * (cpRealSizeinfo.bzkd + cpRealSizeinfo.bzgd);
				var isOversize = Tocountry == "US" && order.details.Sum(p => p.sl) == 1 && (cpRealSizeinfo.bzcd > 96 || girth > 130) && cpRealSizeinfo.bzcd < 108 && girth < 165 && cpRealSizeinfo.weight < 150;


				var hasThirdBillAccount = _ThirdBillAccountList.Any(p => p.Logistics == "Fedex" && p.CountryCode == order.country && p.Shop == order.dianpu);

				var warehouseLogisticFeeList = new List<WarehouseLogisticFee>();

				//获取Xmile运费
				var xmile_warehouses = logisticsmodes.First(p => p.logisticsName == "Xmile").warehouseIds;
				var xmileFeeinfo_merge = GetXmileFee_merge(order, MatchRules, logisticsmodes, xmile_warehouses, EffectiveWarehouse);
				//var xmileFee_split_totalFee = 0m;
				//var xmileFeeinfo = GetXmileFee_split(order, MatchRules, logisticsmodes, xmile_warehouses, EffectiveWarehouse, ref xmileFee_split_totalFee);
				//var XmileWarehouseIdRule = MatchRules.FirstOrDefault(p => p.Type == "XmileProductId");
				//var XmileWarehouseIdRuleDetails = db.scb_WMS_MatchRule_Details.Where(p => p.RID == XmileWarehouseIdRule.ID);
				Helper.Info(order.number, "wms_api", $"获取xmile费用:{JsonConvert.SerializeObject(xmileFeeinfo_merge.Item2)}。message:{xmileFeeinfo_merge.Item1}");

				//获取Roadie运费
				var roadie_warehouses = logisticsmodes.First(p => p.logisticsName == "Roadie").warehouseIds;
				var roadie_totalFee = 0m;
				var roadieFeeinfo = GetRoadieFee(order, MatchRules, logisticsmodes, roadie_warehouses, EffectiveWarehouse, ref roadie_totalFee);
				Helper.Info(order.number, "wms_api", $"获取Roadie费用:{JsonConvert.SerializeObject(roadieFeeinfo.Item2)}。message:{roadieFeeinfo.Item1}");

				//获取GOFO运费
				var GOFOFeeinfo = GetGOFOFee(order, MatchRules, logisticsmodes, EffectiveWarehouse, cpRealSizeinfos);
				Helper.Info(order.number, "wms_api", $"获取GOFO费用:{JsonConvert.SerializeObject(GOFOFeeinfo.Item2)}。message:{GOFOFeeinfo.Item1}");

				//获取Fimile运费
				var FimileFeeinfo = GetFimileFee(order, MatchRules, logisticsmodes, EffectiveWarehouse);
				Helper.Info(order.number, "wms_api", $"获取Fimile费用:{JsonConvert.SerializeObject(FimileFeeinfo.Item2)}。message:{FimileFeeinfo.Item1}");

				//获取Westnovo运费
				var WestnovoFeeinfo = GetWestnovoFee(order, MatchRules, logisticsmodes, EffectiveWarehouse);
				Helper.Info(order.number, "wms_api", $"获取Westnovo费用:{JsonConvert.SerializeObject(WestnovoFeeinfo.Item2)}。message:{WestnovoFeeinfo.Item1}");

				//获取Ontrac运费CanSendOntrac
				var OntracFeeinfo = CanSendOntrac(order, MatchRules, isOversize);
				//Helper.Info(order.number, "wms_api", $"Ontrac发货情况:{OntracFeeinfo.Item1}。message:{JsonConvert.SerializeObject(OntracFeeinfo.Item2)}");

				//20250625 余芳芳 这些邮箱的客人麻烦设置成只发fedex和ups哈
				//20250626 余芳芳 alipu.huang@weifeikj.com 这个邮箱的客人也帮忙设置成只发ups和fedex
				var onlyFedexUPSEmails = "alipu.huang@weifeikj.com,sunny@wellforgroup.com,sskyn36@outlook.com,tarun@slickblue.com,2885382344@qq.com,wangyg@nbhooya.com,Fawz3266@gmail.com".Split(',').ToList();
				var isOnlyFedexUPS = onlyFedexUPSEmails.Any(p => p.ToLower() == order.email.ToLower());

				//转换为物流和仓库，restock-2块，排除 US10 和UPS
				foreach (var warehouse in EffectiveWarehouse)
				{
					var isrestock = restockck.Any(p => p.id == warehouse.id);
					var zone = warehouse.GNDZone;
					var logistics = logisticsmode_FeeList.Where(p => p.zone == zone);
					foreach (var item in logistics)
					{
						//这些物流要单独计算运费，所以不用os刷的费用
						if (item.LogisticsType.Contains("OnTrac") || item.LogisticsType.Contains("Xmile") || item.LogisticsType.Contains("Roadie") || item.LogisticsType.Contains("WESTERN_POST") || item.LogisticsType.Contains("GOFO") || item.LogisticsType.Contains("Fimile"))
						{
							continue;
						}
						//20251020 1-12OZ按重量匹配 13-15OZ 就是哪个便宜发哪个
						if (!canUSPS && item.LogisticsType.Contains("USPS"))
						{
							continue;
						}
						//redtock运费降低2块
						if (isrestock)
						{
							item.fee -= 2;
						}
						//US10不发UPS，排除了
						if ((warehouse.id == "US10" || warehouse.id == "US17" || warehouse.id == "US04" || warehouse.id == "US136" || warehouse.id == "US78") && item.LogisticsType.Contains("UPS"))
						{
							continue;
						}
						//target不发surepost，排除了
						if (order.xspt == "Target" && item.LogisticsType.Contains("Surepost"))
						{
							continue;
						}

						//US15 只发fedex
						if (warehouse.id == "US15" && !(item.LogisticsType.ToUpper().Contains("FEDEX") || item.LogisticsType.Contains("AMZN")))
						{
							continue;
						}

						//能否发-3rd
						//20250213 武金凤 平台或店铺有fedex的付款账号，也改成三方发
						if (item.LogisticsType.Contains("FedEx-3rd"))
						{
							//if (isOversize
							//////	Tocountry == "US" && order.details.Sum(p => p.sl) == 1
							//////&& (cpRealSizeinfo.bzcd > 96 || girth > 130) && cpRealSizeinfo.bzcd < 108 && girth < 165 && cpRealSizeinfo.weight < 150
							////&& !hasThirdBillAccount  //OrderMatch.SericeType.Contains("FEDEX") &&
							//&& fedex3rd_warehouses.Contains(warehouse.id)  //EffectiveWarehouse.Any(p => fedex3rd_warehouses.Contains(p.id)))
							//)
							//{
							//	//US12 打单尺寸在130以下不发
							//	//20240626 张洁楠 US09 只能发FedExOVERSIZE
							//	//20250212 张洁楠 09仓库打单尺寸在130以下的原本就是能发的，推GASA-FEDEX-GROUND这个渠道，现在12仓库打单尺寸在130以下的也能发，这个推FEDEXOVERSIZE-HY-HUSTON这个渠道
							//	//if ((warehouse.id == "US12" || warehouse.id == "US09") && volNum <= 130)
							//	//{
							//	//	continue;
							//	//}
							//	//else if (fedex3rd_warehouses.Contains(warehouse.id))
							//	//{
							//	//	OrderMatch.SericeType = "FedEx-3rd";
							//	//	OrderMatch.Logicswayoid = 121;
							//	//}

							//}
							//else
							//{
							//	continue;
							//}

							if (isOversize && item.LogisticsType != "FedEx-3rd" && fedex3rd_warehouses.Any(p => p.logisticsName == item.LogisticsType && p.warehouseIds.Contains(warehouse.id)))
							{

							}
							else
							{
								continue;
							}

						}

						warehouseLogisticFeeList.Add(new WarehouseLogisticFee()
						{
							ck = warehouse,
							LogisticsID = item.LogisticsID,
							LogisticsType = item.LogisticsType,
							fee = item.fee
						});
					}

					//Westnovo 
					var itemWestnovoFeeinfo = WestnovoFeeinfo.Item2.ContainsKey(warehouse.id) ? WestnovoFeeinfo.Item2[warehouse.id] : 0;  //.Where(p => p.Key == warehouse.id).FirstOrDefault();
					if (itemWestnovoFeeinfo > 0)
					{
						warehouseLogisticFeeList.Add(new WarehouseLogisticFee()
						{
							ck = warehouse,
							LogisticsID = 137,
							LogisticsType = "WESTERN_POST",
							fee = itemWestnovoFeeinfo, // xmileFeeinfo.Item2
													   //totalfee = xmileFee_split_totalFee
						});
					}

					//GOFO 
					var itemGOFOFeeinfo = GOFOFeeinfo.Item2.ContainsKey(warehouse.id) ? GOFOFeeinfo.Item2[warehouse.id] : null;  //.Where(p => p.Key == warehouse.id).FirstOrDefault();
					if (itemGOFOFeeinfo != null)
					{
						foreach (var item in itemGOFOFeeinfo)
						{
							warehouseLogisticFeeList.Add(new WarehouseLogisticFee()
							{
								ck = warehouse,
								LogisticsID = item.LogisticsID,
								LogisticsType = item.LogisticsType,
								fee = item.fee,
							});
						}
					}

					//Fimile 
					var itemFimileFeeinfo = FimileFeeinfo.Item2.ContainsKey(warehouse.id) ? FimileFeeinfo.Item2[warehouse.id] : 0;  //.Where(p => p.Key == warehouse.id).FirstOrDefault();
					if (itemFimileFeeinfo > 0)
					{
						warehouseLogisticFeeList.Add(new WarehouseLogisticFee()
						{
							ck = warehouse,
							LogisticsID = 148,
							LogisticsType = "Fimile",
							fee = itemFimileFeeinfo,
						});
					}


					//Xmile不多包发
					var itemXmileFeeinfo = xmileFeeinfo_merge.Item2.ContainsKey(warehouse.id) ? xmileFeeinfo_merge.Item2[warehouse.id] : 0;  //.Where(p => p.Key == warehouse.id).FirstOrDefault();
					if (itemXmileFeeinfo > 0 && !isOnlyFedexUPS)//&& order.details.Sum(p => p.sl) == 1
					{
						warehouseLogisticFeeList.Add(new WarehouseLogisticFee()
						{
							ck = warehouse,
							LogisticsID = 136,
							LogisticsType = "Xmile",
							fee = itemXmileFeeinfo, // xmileFeeinfo.Item2
							totalfee = itemXmileFeeinfo * order.details.Sum(p => p.sl)
						});
					}

					// roadie 合并发货
					var itemRoadieFeeinfo = roadieFeeinfo.Item2.ContainsKey(warehouse.id) ? roadieFeeinfo.Item2[warehouse.id] : 0;  //.Where(p => p.Key == warehouse.id).FirstOrDefault();
					if (itemRoadieFeeinfo > 0 && !isOnlyFedexUPS)
					{
						//20250730 武金凤 Roadie运费-$1后参与打单匹配
						warehouseLogisticFeeList.Add(new WarehouseLogisticFee()
						{
							ck = warehouse,
							LogisticsID = 141,
							LogisticsType = "Roadie",
							fee = order.details.Sum(p => p.sl) > 1 ? itemRoadieFeeinfo : itemRoadieFeeinfo - 1,// xmileFeeinfo.Item2
							totalfee = roadie_totalFee - 1
						});
					}

					//Ontrac
					#region 获取Ontrac运费
					var OntracMessage = OntracFeeinfo.Item2;
					if (OntracFeeinfo.Item1 && !isOnlyFedexUPS)
					{
						var zone_ontrac = _cacheOntracZip.FirstOrDefault(p => p.IsValid == 1 && p.OriginWarehouse == warehouse.id && order.zip.StartsWith(p.DestZip));
						if (zone_ontrac != null)
						{
							if (!OntracFeeinfo.Item3.Contains(warehouse.id))
							{
								OntracMessage = $"{warehouse.id} 今天订单量已达上限，不再匹配Ontrac物流;";
							}
							else
							{
								var logistics_ontrac = db.scb_logisticsForproduct_us_new.Where(p => p.IsChecked == 1 && p.LogisticsID == 144 && p.goods == cpbh && zone_ontrac.GNDZone == p.zone).OrderBy(p => p.fee).ToList();
								foreach (var item in logistics_ontrac)
								{
									//20250714 武金凤 US11 Borden的FedEx单量上限3000单 其他单子转Ontrac物流发
									bool fedexCheckCount = true;
									if (warehouse.id == "US11")
									{
										var date = DateTime.Now.Date;
										var fedex_coun = db.Database.SqlQuery<int>($"select count(1) from scb_xsjl(nolock) where newdateis > '{date}'  and lywl = '邮寄' and  warehouseid = 82 and (servicetype = 'FEDEX_GROUND'  or logicswayoid = 18) and state=0 and (filterflag >5 or (filterflag =4 and ISNULL(trackno,'')<> '')) and country = 'US' and temp10 ='US'").FirstOrDefault();
										if (fedex_coun != null && fedex_coun >= 3000)
										{
											fedexCheckCount = false;
											OntracMessage += $"US11 FedEx单量上限3000单,{fedex_coun},转Ontrac物流发;";
										}
									}
									//20250919 武金凤 实际重不超过50lbs，且实际长不超过48，且实际宽不超过30，且实际周长不超过105。实际重56lbs以上且100lbs以下，除oversize，长超72
									if ((weightReal <= 50 && cpRealSizeinfo.bzcd <= 48 && cpRealSizeinfo.bzkd < 30 && girth <= 105))
									{
										//和fedex比价，发便宜的
										var fedex_Fee = warehouseLogisticFeeList.FirstOrDefault(p => p.ck.id == warehouse.id && p.LogisticsID == 18);
										if (fedex_Fee == null || !fedexCheckCount || (fedex_Fee != null && fedex_Fee.fee > item.fee))
										{
											warehouseLogisticFeeList.Add(new WarehouseLogisticFee()
											{
												ck = warehouse,
												LogisticsID = item.LogisticsID,
												LogisticsType = item.LogisticsType,
												fee = item.fee //+ 1000   //amzn,roadie,xmile优先级更高，华送不送这个范围内的货，所以可通过调价来调整优先级
											});
											warehouseLogisticFeeList.RemoveAll(p => p.ck.id == warehouse.id && (p.LogisticsID == 18 || p.LogisticsID == 24));
										}
										OntracMessage += $"OnTrac_zone:{zone_ontrac.GNDZone},OnTrac_fee:{item.fee},FEDEX_fee:{fedex_Fee?.fee},weightReal:{weightReal}:{JsonConvert.SerializeObject(logistics_ontrac)}";//日志记下，好查问题
									}
									//20250916 莫有缘 长超72不能发ontrac
									//20250928 武金凤 去除体积超过 17280 ，体积重 150lb 以上，实际长超过72
									else if (weightReal >= 56 && weightReal <= 100 && cpRealSizeinfo.bzcd <= 72 && volNum2 < 17280 && weight_vol < 150)
									{
										//和fedex比价，发便宜的
										var fedex_Fee = warehouseLogisticFeeList.FirstOrDefault(p => p.ck.id == warehouse.id && p.LogisticsID == 18);
										if (fedex_Fee == null || !fedexCheckCount || (fedex_Fee != null && fedex_Fee.fee > item.fee))
										{
											warehouseLogisticFeeList.Add(new WarehouseLogisticFee()
											{
												ck = warehouse,
												LogisticsID = item.LogisticsID,
												LogisticsType = item.LogisticsType,
												fee = item.fee
											});
											warehouseLogisticFeeList.RemoveAll(p => p.ck.id == warehouse.id && (p.LogisticsID == 18 || p.LogisticsID == 24));
										}
										OntracMessage += $"OnTrac_zone:{zone_ontrac.GNDZone},OnTrac_fee:{item.fee},FEDEX_fee:{fedex_Fee?.fee},weightReal:{weightReal}:{JsonConvert.SerializeObject(logistics_ontrac)}";//日志记下，好查问题
									}
									else
									{
										OntracMessage += $"OnTrac_zone:{zone_ontrac.GNDZone},OnTrac_fee:{item.fee},weightReal:{weightReal},重量范围外;";
									}
								}
							}
						}
						else
						{
							OntracMessage = $"zip:{order.zip} 仓库：{warehouse.id}未获取到Ontrac的Zone,不在可发范围;";
						}
					}
					Helper.Info(order.number, "wms_api", $"Ontrac发货情况:{OntracFeeinfo.Item1}。message:{OntracMessage}");

					//if (OntracFeeinfo.Item2.ContainsKey(warehouse.id) && order.dianpu.ToLower() != "fdsus-bp" && !isOnlyFedexUPS)
					//{
					//	warehouseLogisticFeeList.Add(new WarehouseLogisticFee()
					//	{
					//		ck = warehouse,
					//		LogisticsID = 144,
					//		LogisticsType = "Ontrac",
					//		fee = 9999,// xmileFeeinfo.Item2
					//	});
					//	//FedEx发Ontrac发货尺寸规则外的
					//	warehouseLogisticFeeList.RemoveAll(p => p.ck.id == warehouse.id && p.LogisticsID == 18);
					//	//UPSSurePost发Ontrac发货尺寸规则外的
					//	warehouseLogisticFeeList.RemoveAll(p => p.ck.id == warehouse.id && p.LogisticsID == 24);
					//}
					#endregion


					// oversize 获取卡车运费
					if (isOversize && !isOnlyFedexUPS)
					{
						var ltlFeeinfo = GetLTLFeeByOS(order.country, order.xspt, order.dianpu, order.city, order.statsa, order.zip, order.khname, order.phone, order.adress, order.address1, warehouse.number.ToString(), warehouse.id, cpExpressSizeinfos);
						Helper.Info(order.number, "wms_api", $"获取LTL费用: {ltlFeeinfo.Item2}。message:{ltlFeeinfo.Item1}");
						if (ltlFeeinfo.Item2 > 0)
						{
							warehouseLogisticFeeList.Add(new WarehouseLogisticFee()
							{
								ck = warehouse,
								LogisticsID = 22,
								LogisticsType = "LTL",
								fee = ltlFeeinfo.Item2,// xmileFeeinfo.Item2
								totalfee = ltlFeeinfo.Item2
							});
						}
					}

				}

				Helper.Info(order.number, "wms_api", "明细，restock运费-2:" + JsonConvert.SerializeObject(warehouseLogisticFeeList));

				if (warehouseLogisticFeeList.Any(a => a.LogisticsType.Contains("Surepost")))
				{
					var cd = cpRealSizeinfos.FirstOrDefault().bzcd;
					if (cd > 60)
					{
						warehouseLogisticFeeList = warehouseLogisticFeeList.Where(p => p.LogisticsType != "Surepost").ToList();
						Helper.Info(order.number, "wms_api", $"Surepost 最长边{cd}>60 尺寸限制," + JsonConvert.SerializeObject(warehouseLogisticFeeList));
					}
				}

				if (isOnlyFedexUPS)
				{
					warehouseLogisticFeeList = warehouseLogisticFeeList.Where(p => p.LogisticsType.ToUpper().StartsWith("UPS") || p.LogisticsType.ToUpper().StartsWith("FEDEX")).ToList();
					Helper.Info(order.number, "wms_api", $"{order.email}邮箱的客人只发fedex和ups," + JsonConvert.SerializeObject(warehouseLogisticFeeList));
				}

				var onlyUps = false;
				//20250626 余芳芳 B2C店铺   TM-   开头的订单就只发UPS
				//20250918 史倩文 B2C店铺   TM-   开头的订单就默认FEDEX
				if (order.xspt == "B2C" && order.dianpu.ToLower() == "dropshop" && order.orderId.ToUpper().StartsWith("TM-"))
				{
					//onlyUps = true;
					warehouseLogisticFeeList = warehouseLogisticFeeList.Where(p => p.LogisticsType.ToUpper().StartsWith("FEDEX")).ToList();
					var message = $"订单号:{order.orderId} B2C平台，dropshop店铺，TM-开头的订单默认发FEDEX ";
					if (warehouseLogisticFeeList.Count == 0)
						OrderMatch.Result.Message += message;
					Helper.Info(order.number, "wms_api", message + JsonConvert.SerializeObject(warehouseLogisticFeeList));
				}

				//20250619 刘志明 FDSUS-BP 店铺只能发UPS或者Fedex
				if (order.dianpu == "FDSUS-BP")
				{
					warehouseLogisticFeeList = warehouseLogisticFeeList.Where(p => p.LogisticsType.ToUpper().StartsWith("UPS") || p.LogisticsType.ToUpper().StartsWith("FEDEX") || p.LogisticsType.ToUpper().StartsWith("AMZN") || p.LogisticsType.ToUpper().StartsWith("ONTRAC")).ToList();
					Helper.Info(order.number, "wms_api", $"FDSUS-BP 店铺只能发UPS或者Fedex或AMZN或部分邮箱支持ontrac," + JsonConvert.SerializeObject(warehouseLogisticFeeList));
				}


				//蒋建涛 20250417 一单多件可以发Xmile，就不用考虑运费
				if (order.details.Sum(p => p.sl) > 1 && warehouseLogisticFeeList.Any(p => p.LogisticsType == "Xmile" || p.LogisticsType == "Roadie"))
				{
					warehouseLogisticFeeList = warehouseLogisticFeeList.Where(p => p.LogisticsType == "Xmile" || p.LogisticsType == "Roadie").OrderBy(p => p.totalfee).ToList();
				}

				#region AMZN
				var canAMZN = false; var AMZNMessage = string.Empty;
				if (IsAMZNXsptOrDianpu(MatchRules, order.xspt, order.dianpu, order.email))
				{
					//属于AMZN平台，但AMZN打不掉，要排除
					if (order.ignoreAMZN)
					{
						warehouseLogisticFeeList = warehouseLogisticFeeList.Where(p => p.LogisticsType != "AMZN").ToList();
						AMZNMessage += "属于AMZN可发平台，但排除AMZN。";
						//Helper.Info(order.number, "wms_api", "属于AMZN可发平台，但排除AMZN:" + JsonConvert.SerializeObject(warehouseLogisticFeeList));
					}
					else
					{
						canAMZN = true;
					}
				}
				if (!canAMZN)
				{
					warehouseLogisticFeeList = warehouseLogisticFeeList.Where(p => p.LogisticsType != "AMZN").ToList();
					Helper.Info(order.number, "wms_api", "不满足AMZN发货规则:" + AMZNMessage + JsonConvert.SerializeObject(warehouseLogisticFeeList));
				}
				else
				{
					//获取AMZN可用仓库列表
					var cklistForAMZN = logisticsmodes.First(p => p.logisticsName == "AMZN").warehouseIds.Split(',').ToList();
					var removeAmzns = new List<WarehouseLogisticFee>();
					var tempList = warehouseLogisticFeeList.Where(p => p.LogisticsType == "AMZN").ToList();
					foreach (var warehouseLogisticFeeitem in tempList)
					{
						if (!CanSendAMZN(cklistForAMZN, MatchRules, warehouseLogisticFeeitem.ck.id, order, Tocountry, CanCAToUS, weightReal, weight, volNum2))
						{
							warehouseLogisticFeeList.Remove(warehouseLogisticFeeitem);
						}

					}
				}
				#endregion


				////20241203 武金凤 UPS参与比价，跟据可发列表过滤物流  临时  注释
				//if (_UPSSkuList.Any(p => cpbhs.Contains(p)) && warehouseLogisticFeeList.Where(p => p.LogisticsType != "Fedex").Any())  //存在ups可发列表
				//{
				//	warehouseLogisticFeeList = warehouseLogisticFeeList.Where(p => p.LogisticsType != "Fedex").ToList();
				//	Helper.Info(order.number, "wms_api", "存在ups可发列表，排除FEDEX:" + JsonConvert.SerializeObject(warehouseLogisticFeeList));
				//}


				//20241122 武金凤 处于合同替换期 ups折扣不确定，除小包裹和指定物流，其他不发UPS
				//20241202 武金凤 ups按哪个便宜发哪个来发
				if (!onlyUps && warehouseLogisticFeeList.Any(p => p.LogisticsType == "UPS" || p.LogisticsType.ToUpper() == "SUREPOST"))
				{
					warehouseLogisticFeeList = warehouseLogisticFeeList.Where(p => !(p.LogisticsType == "UPS" || p.LogisticsType.ToUpper() == "SUREPOST")).ToList();
					Helper.Info(order.number, "wms_api", "处于合同替换期 ups折扣不确定，除小包裹和指定物流，其他不发UPS:" + JsonConvert.SerializeObject(warehouseLogisticFeeList));
				}
				var json = JsonConvert.SerializeObject(warehouseLogisticFeeList);
				//把能用的都剩下来了，然后找个最便宜的
				var warehouseLogisticFee = warehouseLogisticFeeList.OrderBy(p => p.fee).ThenBy(p => p.ck.GNDZone).ThenBy(p => p.ck.OriginSort).FirstOrDefault();
				if (warehouseLogisticFee != null)
				{
					OrderMatch.WarehouseID = warehouseLogisticFee.ck.number;
					OrderMatch.WarehouseName = warehouseLogisticFee.ck.id;
					OrderMatch.Logicswayoid = (int)warehouseLogisticFee.LogisticsID;
					OrderMatch.SericeType = GetUSServceType((int)warehouseLogisticFee.LogisticsID, warehouseLogisticFee.LogisticsType);
					OrderMatch.MatchFee = warehouseLogisticFee.fee;
					IsFind2 = true;
				}
				else
				{
					IsFind2 = false;
					OrderMatch.WarehouseID = 0;
					OrderMatch.WarehouseName = "";
					OrderMatch.Logicswayoid = 0;
					OrderMatch.SericeType = "";
					OrderMatch.Result.Message = "匹配失败，无可用物流，请确认";
				}
			}
			return IsFind2;
		}

		//打单种及打单尺寸
		/// <summary>
		/// message，dic<warehouse,fee>
		/// </summary>
		/// <param name="order"></param>
		/// <param name="MatchRules"></param>
		/// <param name="logisticsmodes"></param>
		/// <param name="xmile_warehouses"></param>
		/// <param name="EffectiveWarehouse"></param>
		/// <returns></returns>
		public Tuple<string, Dictionary<string, decimal>> GetXmileFee_split(MatchModel order, List<scb_WMS_MatchRule> MatchRules, List<LogisticsWareRelationModel> logisticsmodes, string xmile_warehouses, List<ScbckModel> EffectiveWarehouses, ref decimal totalfee)
		{
			var db = new cxtradeModel();
			var result = new Dictionary<string, decimal>();
			#region 校验是否能发Xmile
			////不多包发
			//if (order.details.Sum(p => p.sl) > 1)
			//{
			//	return new Tuple<string, Dictionary<string, decimal>>("合并发货不支持", result);
			//}

			//判断邮编，平台是否支持
			var xsptRule = MatchRules.FirstOrDefault(p => p.Type == "XmileXsptConfine");
			var xmileXsptInfos = db.scb_WMS_MatchRule_Details.Where(p => p.RID == xsptRule.ID).ToList();
			var walmart_exclude_Dianpus = "walmartmexico,walmartfw,walmartgw".Split(',').ToList();
			if (!xmileXsptInfos.Any(p => (p.Parameter.ToLower() == order.xspt.ToLower() && p.Type == "platform") || (p.Parameter.ToLower() == order.dianpu.ToLower() && p.Type == "store"))
				|| (order.xspt.ToLower() == "walmart" && walmart_exclude_Dianpus.Any(p => p.ToLower() == order.dianpu.ToLower()))
				)
			{
				//throw new Exception("店铺或平台不支持");
				return new Tuple<string, Dictionary<string, decimal>>("店铺或平台不支持", result);
			}

			if (order.statsa.Length > 2)
			{
				var ios2 = db.scb_ProvinceISO2.Where(a => a.country == "US" && order.statsa.Contains(a.statsa)).FirstOrDefault();
				if (ios2 != null)
				{
					order.statsa = ios2.ISO2;
				}
			}
			var xmilezip = db.scb_xmile_zip.Where(a => a.IsValid == 1 && order.zip.StartsWith(a.DestZip) && order.statsa.ToUpper() == a.State).Select(p => p.OriginWarehouse).ToList();
			if (xmilezip.Count == 0)
			{
				//hrow new Exception($"zip:{order.zip}不支持");
				return new Tuple<string, Dictionary<string, decimal>>($"zip:{order.zip}不支持", result);
			}
			////过滤不能发的仓库
			//var XmileWarehouseIdRule = MatchRules.FirstOrDefault(p => p.Type == "XmileProductId");
			//var XmileWarehouseIdRuleDetails = db.scb_WMS_MatchRule_Details.Where(p => p.RID == XmileWarehouseIdRule.ID && p.Type == order.statsa).Select(p => p.Parameter).ToList(); //对应分区的可发仓库
			//if (XmileWarehouseIdRuleDetails.Count == 0)
			//{
			//	return new Tuple<string, Dictionary<string, decimal>>($"{order.statsa}未配置可发仓分区", result);
			//}

			EffectiveWarehouses = EffectiveWarehouses.Where(p => xmilezip.Contains(p.id)).ToList();
			if (EffectiveWarehouses.Count == 0)
			{
				return new Tuple<string, Dictionary<string, decimal>>($"对应分区仓库{string.Join(",", xmilezip)},无可用库存", result);
			}
			#endregion

			var sheet = order.details.First();
			var pieceFee = 0m;
			var message = "";

			#region 合并发货计费规则  注释
			////销售记录表的订单
			//var allCpbhs = db.Database.SqlQuery<string>($@"select CONCAT(b.sl, '*', b.cpbh,'*',a.number,'*',a.warehouseid,'*',case when filterflag >= 7 then  a.logicswayoid when filterflag =4 and ISNULL(a.trackno,'')!='' then  a.logicswayoid else '' end) from scb_xsjl a,scb_xsjlsheet b 
			//where a.number =b.father and a.temp3 = '0' and a.state=0 and orderid = '{order.orderId}' ").ToList();
			////			var allCpbhs = db.Database.SqlQuery<XMileMatchModel>($@"select a.number nid, b.sl,cpbh,(case when filterflag >= 7 then  1  when filterflag =4 and ISNULL(a.trackno,'')!='' then  1 else 0 end) isMatch,logicswayoid,warehouseid
			////from scb_xsjl a,scb_xsjlsheet b where a.number =b.father and a.temp3 = '0' and a.state=0 and orderid = '{order.orderId}' ").ToList();
			////预处理没同步的订单
			//var preallCpbhs = db.Database.SqlQuery<string>($@"select CONCAT(b.sl, '*', b.cpbh,'*',a.id,'*',a.warehouseid,'*',case when filterflag >= 7 then  a.logicswayoid when filterflag =4 and ISNULL(a.trackno,'')!='' then  a.logicswayoid else '' end) from pre_scb_xsjl a
			//left join pre_scb_xsjlsheet b on  a.id =b.father
			//left join scb_PreRelatedXsjl c on c.PreId = a.id
			//where c.PreId is null and a.temp3 = '0' and a.state=0 and orderid = '{order.orderId}' ").ToList();
			////			var preallCpbhs = db.Database.SqlQuery<XMileMatchModel>($@"select  a.id nid, b.sl,cpbh,(case when filterflag >= 7 then  1  when filterflag =4 and ISNULL(a.trackno,'')!='' then  1 else 0 end) isMatch,logicswayoid,warehouseid
			////from pre_scb_xsjl a
			////left join pre_scb_xsjlsheet b on  a.id =b.father
			////left join scb_PreRelatedXsjl c on c.PreId = a.id
			////where c.PreId is null and a.temp3 = '0' and a.state=0 and orderid = '{order.orderId}'").ToList();
			//allCpbhs.AddRange(preallCpbhs);

			//var canSendXmailCps = new List<string>();

			//foreach (var effectiveWarehouse in EffectiveWarehouses)
			//{
			//	var pieceFeeList = new List<decimal>();
			//	foreach (var item in allCpbhs)
			//	{
			//		var itemcp = item.Split('*');
			//		var sl = int.Parse(itemcp[0]);
			//		var cpbh = itemcp[1];
			//		var isxmile = false;
			//		////先排除自己，然后看当前这个是否匹配
			//		var ismatch = itemcp.Count() > 3 && itemcp[2] != order.number && itemcp[4] != "" && itemcp[4] != "0";

			//		#region 获取cpbh尺寸信息
			//		var good = db.N_goods.Where(p => p.cpbh == cpbh && p.country == order.country).FirstOrDefault();
			//		var size = new List<double>() { (double)good.bzcd, (double)good.bzkd, (double)good.bzgd };
			//		var weight = (double)good.weight_express / 1000 * 2.2046226f;
			//		size.Sort();//从小到大排序
			//		var length = size[2];
			//		var width = size[1];
			//		var height = size[0];
			//		var girth = length + 2 * (width + height);
			//		#endregion

			//		//var canDiscount = false;  //是否第二件半价

			//		if (ismatch)
			//		{
			//			if (itemcp[3] == effectiveWarehouse.number.ToString() && itemcp[4] == "136")
			//			{
			//				//canDiscount = true;
			//			}
			//			else
			//			{
			//				message += $"货号:{cpbh},已匹配物流:{itemcp[4]},仓库:{itemcp[3]}<>{effectiveWarehouse.number} 不参与运费计算;";
			//				continue;
			//			}
			//		}
			//		else //未匹配。就看有无指定仓，有可发仓库存
			//		{
			//			//同款替换是否有指定仓
			//			var cpbhReplaceInfo = db.scb_order_cpbh_Replace.Where(p => p.orderId == order.orderId && p.replaceType == WMSClassLibrary.Enums.EnumReplaceType.单品替换 && p.thhh == cpbh && !string.IsNullOrEmpty(p.appointWare)).OrderByDescending(p => p.createTime).FirstOrDefault();
			//			if (cpbhReplaceInfo != null)
			//			{
			//				if (cpbhReplaceInfo.appointWare != effectiveWarehouse.id)
			//				{
			//					message += $"货号:{cpbh},同款替换指定仓:{cpbhReplaceInfo.appointWare} <> {effectiveWarehouse.id};";
			//					continue;
			//				}
			//				else
			//				{
			//					TempKcslHelper tempKcslHelper = new TempKcslHelper();
			//					var checkWare = new List<string> { effectiveWarehouse.id };
			//					//判断库存
			//					if (!tempKcslHelper.IsGoodsEnough(cpbh, order.toCountry, DateTime.Now.ToString("yyyy-MM-dd"), sl - 1, order.country, checkWare).Item1)
			//					{
			//						message += $"货号:{cpbh},可发仓:{effectiveWarehouse.id}无库存;";
			//						continue;
			//					}
			//				}
			//			}
			//		}
			//		try
			//		{
			//			var itempieceFee = GetXmilePieceFee(length, width, height, girth, weight);
			//			message += $"货号:{sl}*{cpbh}，{itempieceFee}$/件;";
			//			canSendXmailCps.Add(cpbh);
			//			for (int i = 0; i < sl; i++)
			//			{
			//				pieceFeeList.Add(itempieceFee);
			//			}
			//		}
			//		catch (Exception ex)
			//		{
			//			message += $"货号:{cpbh},{ex.Message};";
			//		}
			//	}

			//	if (order.details.All(p => canSendXmailCps.Contains(p.cpbh)))
			//	{
			//		//第一件，选最贵的，便宜的第n件半价
			//		pieceFeeList = pieceFeeList.OrderByDescending(p => p).ToList();
			//		var trackfee = -1m;
			//		if (pieceFeeList.Any())
			//		{
			//			for (int i = 0; i < pieceFeeList.Count; i++)
			//			{

			//				if (i == 0)
			//				{
			//					trackfee = pieceFeeList[i];
			//				}
			//				else
			//				{
			//					//trackfee += pieceFeeList[i] / 2;
			//					trackfee += pieceFeeList[i];
			//				}
			//			}
			//			trackfee = trackfee / pieceFeeList.Count;
			//			result.Add(effectiveWarehouse.id, trackfee);
			//		}
			//	}
			//}
			#endregion

			#region  分开计费了

			//计算每个货号的费用
			var cpbhs = order.details.Select(p => p.cpbh).Distinct().ToList();
			var cp_Price_dic = new Dictionary<string, decimal>();
			foreach (var cpbh in cpbhs)
			{
				if (cp_Price_dic.ContainsKey(cpbh))
				{
					continue;
				}
				#region 获取cpbh尺寸信息
				var good = db.N_goods.Where(p => p.cpbh == cpbh && p.country == order.country).FirstOrDefault();
				var size = new List<double>() { (double)good.bzcd, (double)good.bzkd, (double)good.bzgd };
				var weight = (double)good.weight_express / 1000 * 2.2046226f;
				size.Sort();//从小到大排序
				var length = size[2];
				var width = size[1];
				var height = size[0];
				var girth = length + 2 * (width + height);
				#endregion

				var itempieceFee = GetXmilePieceFee_split(length, width, height, girth, weight);
				cp_Price_dic.Add(cpbh, itempieceFee);
				message += $"货号:{cpbh}，{itempieceFee}$/件;";

				//计算总金额
				totalfee += itempieceFee * order.details.Where(p => p.cpbh == cpbh).Sum(p => p.sl);
			}

			//更新仓库的费用
			var fee = totalfee / order.details.Sum(p => p.sl);
			foreach (var effectiveWarehouse in EffectiveWarehouses)
			{
				if (xmilezip.Contains(effectiveWarehouse.id))
				{
					result.Add(effectiveWarehouse.id, fee);
				}
				else
				{
					message += $"邮编:{order.zip} 仓库:{effectiveWarehouse.id} 非Xmile 可发仓库";
				}
			}
			#endregion

			return new Tuple<string, Dictionary<string, decimal>>(message, result);
		}

		public Tuple<string, Dictionary<string, decimal>> GetXmileFee_merge(MatchModel order, List<scb_WMS_MatchRule> MatchRules, List<LogisticsWareRelationModel> logisticsmodes, string xmile_warehouses, List<ScbckModel> EffectiveWarehouses)
		{
			var db = new cxtradeModel();
			var result = new Dictionary<string, decimal>();
			#region 校验是否能发Xmile
			////不多包发
			//if (order.details.Sum(p => p.sl) > 1)
			//{
			//	return new Tuple<string, Dictionary<string, decimal>>("合并发货不支持", result);
			//}

			//判断邮编，平台是否支持
			var xsptRule = MatchRules.FirstOrDefault(p => p.Type == "XmileXsptConfine");
			var xmileXsptInfos = db.scb_WMS_MatchRule_Details.Where(p => p.RID == xsptRule.ID).ToList();
			var walmart_exclude_Dianpus = "walmartmexico,walmartfw,walmartgw".Split(',').ToList();
			if (!xmileXsptInfos.Any(p => (p.Parameter.ToLower() == order.xspt.ToLower() && p.Type == "platform") || (p.Parameter.ToLower() == order.dianpu.ToLower() && p.Type == "store"))
				|| (order.xspt.ToLower() == "walmart" && walmart_exclude_Dianpus.Any(p => p.ToLower() == order.dianpu.ToLower()))
				)
			{
				//throw new Exception("店铺或平台不支持");
				return new Tuple<string, Dictionary<string, decimal>>("店铺或平台不支持", result);
			}

			var canSendZips = _cacheXmileZip.Where(p => p.IsValid == 1 && order.zip.StartsWith(p.DestZip) && order.statsa.ToUpper() == p.State).ToList();
			if (canSendZips.Count == 0)
			{
				return new Tuple<string, Dictionary<string, decimal>>($"zip:{order.zip}不支持", result);
			}
			var canSendCks = canSendZips.Select(p => p.OriginWarehouse).Distinct().ToList();
			EffectiveWarehouses = EffectiveWarehouses.Where(p => canSendCks.Contains(p.id)).ToList();
			////20250609 蒋建涛 US12 houston要转回以前的第二件半价的形式
			//EffectiveWarehouses = EffectiveWarehouses.Where(p => p.id == "US12").ToList();

			if (EffectiveWarehouses.Count == 0)
			{
				return new Tuple<string, Dictionary<string, decimal>>($"对应分区仓库{string.Join(",", canSendCks)},无可用库存", result);
			}
			#endregion

			var sheet = order.details.First();
			var pieceFee = 0m;
			var message = "";

			#region 合并发货计费规则 
			////销售记录表的订单
			//var allCpbhs = db.Database.SqlQuery<string>($@"select CONCAT(b.sl, '*', b.cpbh,'*',a.number,'*',a.warehouseid,'*',case when filterflag >= 7 then  a.logicswayoid when filterflag =4 and ISNULL(a.trackno,'')!='' then  a.logicswayoid else '' end) from scb_xsjl a,scb_xsjlsheet b 
			//where a.number =b.father and a.temp3 = '0' and a.state=0 and orderid = '{order.orderId}' ").ToList();
			////预处理没同步的订单
			//var preallCpbhs = db.Database.SqlQuery<string>($@"select CONCAT(b.sl, '*', b.cpbh,'*',a.id,'*',a.warehouseid,'*',case when filterflag >= 7 then  a.logicswayoid when filterflag =4 and ISNULL(a.trackno,'')!='' then  a.logicswayoid else '' end) from pre_scb_xsjl a
			//left join pre_scb_xsjlsheet b on  a.id =b.father
			//left join scb_PreRelatedXsjl c on c.PreId = a.id
			//where c.PreId is null and a.temp3 = '0' and a.state=0 and orderid = '{order.orderId}' ").ToList();
			//allCpbhs.AddRange(preallCpbhs);

			var cpbhs = order.details.Select(p => p.cpbh).Distinct().ToList();

			var canSendXmailCps = new List<string>();

			foreach (var effectiveWarehouse in EffectiveWarehouses)
			{
				var zone = canSendZips.Where(p => p.OriginWarehouse == effectiveWarehouse.id).Select(p => p.GNDZone).First();
				var pieceFeeList = new List<decimal>();
				double sumweight = 0;
				foreach (var item in cpbhs)
				{
					var sl = order.details.Where(p => p.cpbh == item).Sum(p => p.sl);
					var cpbh = item;
					var isxmile = false;

					#region 获取cpbh尺寸信息
					var good = db.N_goods.Where(p => p.cpbh == cpbh && p.country == order.country).FirstOrDefault();
					var size = new List<double>() { (double)good.bzcd, (double)good.bzkd, (double)good.bzgd };
					var weight = (double)good.weight_express / 1000 * 2.2046226f;
					size.Sort();//从小到大排序
					var length = size[2];
					var width = size[1];
					var height = size[0];
					var girth = length + 2 * (width + height);
					#endregion
					sumweight += weight * sl;

					//同款替换是否有指定仓
					var cpbhReplaceInfo = db.scb_order_cpbh_Replace.Where(p => p.orderId == order.orderId && p.replaceType == WMSClassLibrary.Enums.EnumReplaceType.单品替换 && p.thhh == cpbh && !string.IsNullOrEmpty(p.appointWare)).OrderByDescending(p => p.createTime).FirstOrDefault();
					if (cpbhReplaceInfo != null)
					{
						if (cpbhReplaceInfo.appointWare != effectiveWarehouse.id)
						{
							message += $"货号:{cpbh},同款替换指定仓:{cpbhReplaceInfo.appointWare} <> {effectiveWarehouse.id};";
							continue;
						}
						else
						{
							TempKcslHelper tempKcslHelper = new TempKcslHelper();
							var checkWare = new List<string> { effectiveWarehouse.id };
							//判断库存
							if (!tempKcslHelper.IsGoodsEnough(cpbh, order.toCountry, DateTime.Now.ToString("yyyy-MM-dd"), sl - 1, order.country, checkWare).Item1)
							{
								message += $"货号:{cpbh},可发仓:{effectiveWarehouse.id}无库存;";
								continue;
							}
						}
					}

					try
					{
						//20251029 蒋建涛 Xmiles新的运费规则从11月1号开始实行，理论运费最终乘算*0.9
						var itempieceFee = DateTime.Now < new DateTime(2025, 11, 1, 0, 0, 0) ? GetXmilePieceFee_merge(length, width, height, girth, weight, zone) : GetXmilePieceFee_merge_11(length, width, height, girth, weight, zone);
						message += $"货号:{sl}*{cpbh}，{itempieceFee}$/件;";
						canSendXmailCps.Add(cpbh);
						for (int i = 0; i < sl; i++)
						{
							pieceFeeList.Add(itempieceFee);
						}
					}
					catch (Exception ex)
					{
						message += $"货号:{cpbh},{ex.Message};";
					}
				}

				if (sumweight > 500)
				{
					return new Tuple<string, Dictionary<string, decimal>>($"重量超过500bl不发xmile", result);
				}

				if (order.details.All(p => canSendXmailCps.Contains(p.cpbh)))
				{
					//第一件，选最贵的，便宜的第n件半价
					pieceFeeList = pieceFeeList.OrderByDescending(p => p).ToList();
					var trackfee = -1m;
					if (pieceFeeList.Any())
					{
						for (int i = 0; i < pieceFeeList.Count; i++)
						{

							if (i == 0)
							{
								trackfee = pieceFeeList[i];
							}
							else
							{
								//trackfee += pieceFeeList[i] / 2;
								trackfee += pieceFeeList[i] / 2;
							}
							//totalfee += trackfee;
						}
						//20250807 蒋建涛 Xmiles的比价运费*0.8
						//20251029 蒋建涛 Xmiles新的运费规则从11月1号开始实行，理论运费最终乘算*0.9
						var ratio = DateTime.Now < new DateTime(2025, 11, 1, 0, 0, 0) ? 0.8m : 0.9m;
						trackfee = trackfee * ratio / pieceFeeList.Count;
						//总运费
						result.Add(effectiveWarehouse.id, trackfee);
					}
				}
			}
			#endregion

			#region  分开计费了  注释

			//var cpbh = sheet.cpbh;
			//#region 获取cpbh尺寸信息
			//var good = db.N_goods.Where(p => p.cpbh == cpbh && p.country == order.country).FirstOrDefault();
			//var size = new List<double>() { (double)good.bzcd, (double)good.bzkd, (double)good.bzgd };
			//var weight = (double)good.weight_express / 1000 * 2.2046226f;
			//size.Sort();//从小到大排序
			//var length = size[2];
			//var width = size[1];
			//var height = size[0];
			//var girth = length + 2 * (width + height);
			//#endregion
			//var itempieceFee = GetXmilePieceFee(length, width, height, girth, weight);
			//message = $"货号:{cpbh}，{itempieceFee}$/件;";

			//foreach (var effectiveWarehouse in EffectiveWarehouses)
			//{
			//	//var canDiscount = false;  //是否第二件半价
			//	result.Add(effectiveWarehouse.id, itempieceFee);
			//}
			#endregion

			return new Tuple<string, Dictionary<string, decimal>>(message, result);
		}

		/// <summary>
		/// 分开发货，包车计费
		/// </summary>
		/// <param name="length"></param>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <param name="girth"></param>
		/// <param name="weight"></param>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		public decimal GetXmilePieceFee_split(double length, double width, double height, double girth, double weight)
		{
			var pieceFee = 0m;
			if (length <= 48 && girth <= 105 && weight <= 50)
			{
				pieceFee = 15;
			}
			//large
			else if (length <= 96 && girth <= 130 && weight <= 90)
			{
				pieceFee = 16;
			}
			//XL
			else if (length <= 108 && girth <= 165 && weight <= 150)
			{
				pieceFee = 28;
			}
			//XXL
			else if (length <= 108 && width <= 60 && height <= 80 && weight <= 500)
			{
				pieceFee = 0.5m * (decimal)weight;
			}
			else
			{
				throw new Exception($"不满足Xmile计费规则：length:{length},width:{width},height:{height},weight:{weight}");
			}
			return pieceFee;
		}
		public enum XmileServiceEnum
		{
			other,
			AH_DIM,
			AH_WT,
			AH_WTMax,
			OS,
			OM,
			OMMax
		}
		public decimal GetXmilePieceFee_merge(double length, double width, double height, double girth, double weight, int? zone)
		{
			if (!zone.HasValue || zone == 0)
			{
				throw new Exception("zone无效");
			}
			#region 派送服务
			var service = XmileServiceEnum.other;
			if (length <= 96 && girth <= 130 && weight <= 50)
			{
				service = XmileServiceEnum.AH_DIM;
			}
			else if (length <= 96 && girth <= 130 && weight <= 90)
			{
				service = XmileServiceEnum.AH_WT;
			}
			else if (length <= 96 && girth <= 130 && weight <= 150)
			{
				service = XmileServiceEnum.AH_WTMax;
			}
			else if (length <= 108 && girth <= 165 && weight <= 150)
			{
				service = XmileServiceEnum.OS;
			}
			else if (length <= 144 && girth <= 225 && weight <= 200)
			{
				service = XmileServiceEnum.OM;
			}
			else if (length <= 144 && girth <= 225 && weight <= 500)
			{
				service = XmileServiceEnum.OMMax;
			}
			#endregion

			var pieceFee = 0m;

			#region 获取运费
			switch (service)
			{
				case XmileServiceEnum.AH_DIM:
					if (zone == 1 || zone == 2)
						pieceFee = 15;
					else if (zone == 3)
						pieceFee = 16.36m;
					else if (zone == 4)
						pieceFee = 17.86m;
					break;
				case XmileServiceEnum.AH_WT:
					if (zone == 1 || zone == 2)
						pieceFee = 15;
					else if (zone == 3)
						pieceFee = 22.7m;
					else if (zone == 4)
						pieceFee = 24.94m;
					break;
				case XmileServiceEnum.AH_WTMax:
					if (zone == 1 || zone == 2)
						pieceFee = 30;
					else if (zone == 3)
						pieceFee = 33.92m;
					else if (zone == 4)
						pieceFee = 37.54m;
					break;
				case XmileServiceEnum.OS:
					if (zone == 1 || zone == 2)
						pieceFee = 45;
					else if (zone == 3)
						pieceFee = 48.92m;
					else if (zone == 4)
						pieceFee = 52.54m;
					break;
				case XmileServiceEnum.OM:
					if (zone == 1 || zone == 2)
						pieceFee = 65;
					else if (zone == 3)
						pieceFee = 80;
					else if (zone == 4)
						pieceFee = 90;
					break;
				case XmileServiceEnum.OMMax:
					if (zone == 1 || zone == 2)
						pieceFee = 65 + (Math.Ceiling((decimal)weight / 100) - 2) * 40;
					else if (zone == 3)
						pieceFee = 80 + (Math.Ceiling((decimal)weight / 100) - 2) * 60;
					else if (zone == 4)
						pieceFee = 90 + (Math.Ceiling((decimal)weight / 100) - 2) * 70;
					break;
				case XmileServiceEnum.other:
					throw new Exception($"不满足Xmile计费规则：length:{length},width:{width},height:{height},weight:{weight}");
					//break;
			}

			#endregion

			return pieceFee;
		}
		public decimal GetXmilePieceFee_merge_11(double length, double width, double height, double girth, double weight, int? zone)
		{
			if (!zone.HasValue || zone == 0)
			{
				throw new Exception("zone无效");
			}
			#region 派送服务
			var service = XmileServiceEnum.other;
			if (length <= 96 && girth <= 130 && weight <= 90)
			{
				service = XmileServiceEnum.AH_WT;
			}
			else if (length <= 96 && girth <= 130 && weight <= 150)
			{
				service = XmileServiceEnum.AH_WTMax;
			}
			else if (length <= 108 && girth <= 165 && weight <= 150)
			{
				service = XmileServiceEnum.OS;
			}
			else if (length <= 144 && girth <= 225 && weight <= 200)
			{
				service = XmileServiceEnum.OM;
			}
			#endregion

			var pieceFee = 0m;

			#region 获取运费
			switch (service)
			{
				case XmileServiceEnum.AH_WT:
					if (zone == 1 || zone == 2)
						pieceFee = 21.2m;
					else if (zone == 3)
						pieceFee = 22.7m;
					else if (zone == 4)
						pieceFee = 24.94m;
					else if (zone == 5)
						pieceFee = 28.47m;
					else if (zone == 6)
						pieceFee = 31.32m;
					else if (zone == 7)
						pieceFee = 34.2m;
					else if (zone == 8)
						pieceFee = 38.42m;
					break;
				case XmileServiceEnum.AH_WTMax:
					if (zone == 1 || zone == 2)
						pieceFee = 31.8m;
					else if (zone == 3)
						pieceFee = 33.92m;
					else if (zone == 4)
						pieceFee = 37.54m;
					else if (zone == 5)
						pieceFee = 40.05m;
					else if (zone == 6)
						pieceFee = 43.65m;
					else if (zone == 7)
						pieceFee = 47.06m;
					else if (zone == 8)
						pieceFee = 50.16m;
					break;
				case XmileServiceEnum.OS:
					if (zone == 1 || zone == 2)
						pieceFee = 47.7m;
					else if (zone == 3)
						pieceFee = 48.92m;
					else if (zone == 4)
						pieceFee = 52.54m;
					else if (zone == 5)
						pieceFee = 59.42m;
					else if (zone == 6)
						pieceFee = 67.52m;
					else if (zone == 7)
						pieceFee = 71.36m;
					else if (zone == 8)
						pieceFee = 77.76m;
					break;
				case XmileServiceEnum.OM:
					if (zone == 1 || zone == 2)
						pieceFee = 68.9m;
					else if (zone == 3)
						pieceFee = 80;
					else if (zone == 4)
						pieceFee = 90;
					else if (zone == 5)
						pieceFee = 145;
					else if (zone == 6)
						pieceFee = 185;
					else if (zone == 7)
						pieceFee = 225;
					else if (zone == 8)
						pieceFee = 245;
					break;
				case XmileServiceEnum.other:
					throw new Exception($"不满足Xmile计费规则：length:{length},width:{width},height:{height},weight:{weight}");
					//break;
			}

			#endregion

			return pieceFee;
		}


		public Tuple<string, Dictionary<string, decimal>> GetRoadieFee(MatchModel order, List<scb_WMS_MatchRule> MatchRules, List<LogisticsWareRelationModel> logisticsmodes, string xmile_warehouses, List<ScbckModel> EffectiveWarehouses, ref decimal totalfee)
		{
			var db = new cxtradeModel();
			var result = new Dictionary<string, decimal>();
			#region 校验是否能发Roadie

			//判断邮编是否支持


			//var zipRule = MatchRules.FirstOrDefault(p => p.Type == "RoadieConfine");
			//var xmileZips = db.scb_WMS_MatchRule_Details.Where(p => p.RID == zipRule.ID).ToList();
			//if (!xmileZips.Any(p => order.zip.StartsWith(p.Parameter) && order.statsa.ToUpper() == p.Type))
			//{
			//    //hrow new Exception($"zip:{order.zip}不支持");
			//    return new Tuple<string, Dictionary<string, decimal>>($"zip:{order.zip}不支持", result);
			//}

			var usefulWarehouses = logisticsmodes.Where(p => p.logisticsName == "Roadie").Select(p => p.warehouseIds).FirstOrDefault();
			if (!string.IsNullOrEmpty(usefulWarehouses))
			{
				EffectiveWarehouses = EffectiveWarehouses.Where(p => usefulWarehouses.Contains(p.id)).ToList();
			}
			if (EffectiveWarehouses.Count == 0)
			{
				return new Tuple<string, Dictionary<string, decimal>>($"可发仓库:{usefulWarehouses},无可用库存", result);
			}
			//判断店铺平台
			if (order.xspt.ToLower() == "lowes" || order.xspt.ToLower() == "tiktok" || (order.xspt.ToLower() == "b2c" && order.dianpu.ToLower() == "snappy"))
			{
				return new Tuple<string, Dictionary<string, decimal>>($"店铺或平台不支持", result);
			}
			#endregion

			var sheet = order.details.First();
			var pieceFee = 0m;
			var message = "";

			var cpbhs = order.details.Select(p => p.cpbh).Distinct().ToList();
			var cpbh_fee_dic = new Dictionary<string, List<decimal>>();
			foreach (var effectiveWarehouse in EffectiveWarehouses)
			{
				if (!_cacheRoadieZip.Where(p => p.OriginWarehouse == effectiveWarehouse.id && p.State == order.statsa.ToUpper() && order.zip.StartsWith(p.DestZip)).Any())
				{
					message += $"仓库：{effectiveWarehouse.id},邮编：{order.zip}不支持;";
					continue;
				}
				var date = DateTime.Now.Date;
				var thresholdDate = new DateTime(2025, 11, 13); // 2025-11-13
				if (DateTime.Now < thresholdDate && (effectiveWarehouse.id == "US06" || effectiveWarehouse.id == "US16"))
				{
					message += $"2025-11-13开启 US06 US16 Roadie;";
					continue;
				}
				if (effectiveWarehouse.id == "US06" || effectiveWarehouse.id == "US16")
				{
					var roadie_coun = db.Database.SqlQuery<int>($"select count(1) from scb_xsjl(nolock) where newdateis > '{date}'  and lywl = '邮寄' and  warehouseid in(24,99) and (servicetype = 'Roadie'  or logicswayoid = 141) and state=0 and (filterflag >5 or (filterflag =4 and ISNULL(trackno,'')<> '')) and country = 'US' and temp10 ='US'").FirstOrDefault();
					if (roadie_coun != null && roadie_coun >= 500)
					{
						message += $"US06 US16单量上限500单;{roadie_coun};";
						continue;
					}
				}

				var cansend = true;
				foreach (var cpbh in cpbhs)
				{
					int sl = order.details.Where(p => p.cpbh == cpbh).Sum(p => p.sl);
					//同款替换是否有指定仓
					var cpbhReplaceInfo = db.scb_order_cpbh_Replace.Where(p => p.orderId == order.orderId && p.replaceType == WMSClassLibrary.Enums.EnumReplaceType.单品替换 && p.thhh == cpbh && !string.IsNullOrEmpty(p.appointWare)).OrderByDescending(p => p.createTime).FirstOrDefault();
					if (cpbhReplaceInfo != null)
					{
						if (cpbhReplaceInfo.appointWare != effectiveWarehouse.id)
						{
							cansend = false;
							message += $"货号:{cpbh},同款替换指定仓:{cpbhReplaceInfo.appointWare} <> {effectiveWarehouse.id};";
							continue;
						}
						else
						{
							TempKcslHelper tempKcslHelper = new TempKcslHelper();
							var checkWare = new List<string> { effectiveWarehouse.id };
							//判断库存
							if (!tempKcslHelper.IsGoodsEnough(cpbh, order.toCountry, DateTime.Now.ToString("yyyy-MM-dd"), sl - 1, order.country, checkWare).Item1)
							{
								cansend = false;
								message += $"货号:{cpbh},可发仓:{effectiveWarehouse.id}无库存;";
								continue;
							}
						}
					}

					#region 如果可以发，就获取运费了
					if (cpbh_fee_dic.ContainsKey(cpbh))
					{
						continue;
					}
					#region 获取cpbh尺寸信息
					try
					{

						var good = db.N_goods.Where(p => p.cpbh == cpbh && p.country == order.country).FirstOrDefault();
						//var weight = (double)good.weight_express / 1000 * 2.2046226f;
						//var size = new List<double>() { (double)good.bzcd, (double)good.bzkd, (double)good.bzgd };

						//20250611 武金凤 用真实尺寸计算运费
						var weight = (double)good.weight / 1000 * 2.2046226f;
						var realSizeInfo = db.scb_realsize.Where(p => p.cpbh == cpbh && p.country == order.country).FirstOrDefault();
						var size = realSizeInfo == null ? new List<double>() { (double)good.bzcd, (double)good.bzkd, (double)good.bzgd } : new List<double>() { (double)realSizeInfo.bzcd, (double)realSizeInfo.bzkd, (double)realSizeInfo.bzgd };
						size.Sort();//从小到大排序
						var length = size[2];
						var width = size[1];
						var height = size[0];
						var girth = length + 2 * (width + height);
						#endregion
						var itempieceFeeList = GetRoadiePieceFee(length, width, height, girth, weight);
						message += $"货号:{sl}*{cpbh}，{string.Join(",", itempieceFeeList)}$/件;";
						cpbh_fee_dic.Add(cpbh, itempieceFeeList);
					}
					catch (Exception ex)
					{
						cansend = false;
						message += $"货号:{sl}*{cpbh}，{ex.Message};";
					}
					#endregion
				}
				if (cansend && cpbh_fee_dic.Any())
				{
					//最贵的作为第一件
					var maxListValueKv = cpbh_fee_dic
						.ToDictionary(kv => kv.Key, kv => kv.Value.Max())
						.OrderByDescending(kv => kv.Value)
						.FirstOrDefault();
					totalfee = maxListValueKv.Value;
					foreach (var dic in cpbh_fee_dic)
					{
						var sl = order.details.Where(p => p.cpbh == dic.Key).Sum(p => p.sl);
						if (dic.Key == maxListValueKv.Key)
						{
							//判断下数量是否多个
							if (sl > 1)
							{
								totalfee += dic.Value.Min() * (sl - 1);
							}
						}
						else
						{
							totalfee += dic.Value.Min() * sl;
						}

					}
					var zsl = order.details.Sum(p => p.sl);
					if (effectiveWarehouse.id == "US16" || effectiveWarehouse.id == "US06")
					{
						message += "US06、16包车费用每单均摊 1$;";
						totalfee += 1m;
					}
					var itemtrackfee = totalfee / zsl;
					result.Add(effectiveWarehouse.id, itemtrackfee);
				}
			}

			return new Tuple<string, Dictionary<string, decimal>>(message, result);
		}
		/// <summary>
		/// 用的打单重量及尺寸
		/// </summary>
		/// <param name="length"></param>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <param name="girth"></param>
		/// <param name="weight"></param>
		/// <returns></returns>
		public List<decimal> GetRoadiePieceFee(double length, double width, double height, double girth, double weight)
		{
			var prices = new List<decimal>();
			var price = 0m;
			if (length <= 48 && girth <= 105 && weight <= 50)
			{
				prices = new List<decimal>() { 10.24m, 2.55m };
			}
			else if (length <= 96 && girth <= 130 && weight <= 150)
			{
				prices = new List<decimal>() { 17.33m, 4.33m };
			}
			else if (length < 108 && girth < 165 && weight < 150)
			{
				prices = new List<decimal>() { 49.3m, 12.32m };
			}
			else if (length < 144 && girth < 225 && weight < 200)
			{
				prices = new List<decimal>() { 70.3m, 18.64m };
			}
			else
			{
				throw new Exception($"不满足Roadie计费规则：length:{length},width:{width},height:{height},weight:{weight}");
			}
			return prices;
		}


		public Tuple<bool, string, string> CanSendOntrac(MatchModel order, List<scb_WMS_MatchRule> MatchRules, bool isOversize)
		{
			//ontrac不发oversize
			//20250926 武金凤 oversize可以发了参与比价
			//if (isOversize)
			//{
			//	return new Tuple<bool, string, string>(false, "oversize不支持", "");
			//}

			//KC56784NA，KC56784BK，EP22276US
			var cpbh1 = "KC56784NA,KC56784BK,EP22276US".Split(',').ToList();
			var startdate = new DateTime(2025, 11, 8, 0, 0, 0);
			var enddate = new DateTime(2025, 12, 9, 0, 0, 0);
			var nowdate = DateTime.Now;
			if (nowdate >= startdate && nowdate < enddate && (order.dianpu == "SafeplusWH" || order.dianpu == "Hsintmall") && (order.details.Any(p => cpbh1.Contains(p.cpbh))))
			{
				return new Tuple<bool, string, string>(false, $"丢包率高，", "店铺：safeplusWH和Hsintmall,货号:KC56784NA,KC56784BK,EP22276US,禁发Ontrac（11.8-12.8");
			}

			var db = new cxtradeModel();
			//判断店铺平台
			var platformConfine = MatchRules.FirstOrDefault(p => p.Type == "OnTracXsptConfine"); //order.details.First().platform;
			var usefulPlatforms = db.scb_WMS_MatchRule_Details.Where(p => p.RID == platformConfine.ID).ToList();
			if ((!usefulPlatforms.Any(p => p.Type == "platform" && p.Parameter.ToLower() == order.xspt.ToLower()) && !usefulPlatforms.Any(p => p.Type == "Store" && p.Parameter.ToLower() == order.dianpu.ToLower())) || order.dianpu.ToLower() == "fdsus-bp")
			{
				return new Tuple<bool, string, string>(false, $"店铺或平台不支持", "");
			}

			if (order.dianpu == "FDSUS-BP" && order.email != "sskyn36@outlook.com")
			{
				return new Tuple<bool, string, string>(false, $"FDSUS-BP 仅支持邮箱sskyn36@outlook.com", "");
			}

			var result = new Dictionary<string, decimal>();
			var canSend = false;
			var message = string.Empty;
			#region 判断是否在规则内
			//var sheet = order.details.First();
			//var good = db.N_goods.Where(p => p.cpbh == sheet.cpbh && p.country == order.country).FirstOrDefault();
			//var realSize = db.scb_realsize.Where(p => p.cpbh == sheet.cpbh && p.country == order.country).FirstOrDefault();

			//var weight_real_bl = (double)good.weight / 1000 * 2.2046226f;
			//var size_real = new List<decimal>() { (decimal)good.bzcd, (decimal)good.bzkd, (decimal)good.bzgd };
			//if (realSize != null)
			//{
			//	size_real = new List<decimal>() { (decimal)realSize.bzcd, (decimal)realSize.bzkd, (decimal)realSize.bzgd };
			//}
			//size_real.Sort();//从小到大排序
			//var length = size_real[2];
			//var width = size_real[1];
			//var height = size_real[0];
			//var girth = length + 2 * (width + height);
			//var message = $"货号:{sheet.cpbh};weight_real_bl:{weight_real_bl},length:{length},width:{width},height:{height}";
			//if ((weight_real_bl <= 50 && length <= 47 && width < 30 && girth <= 105) || (weight_real_bl >= 56 && weight_real_bl <= 100))
			//{
			//	canSend = true;
			//}
			#endregion

			#region  //20250917 武金凤 取消上限 //且US16Redlands日单量上限1700单，US06Fontana日单量上限700单，US11Borden日单量上限700单
			var cansendWare = string.Empty;//未达上限的仓库们
			cansendWare = string.Join(",", _cacheOntracZip.Where(p => p.IsValid == 1 && order.zip.StartsWith(p.DestZip)).Select(p => p.OriginWarehouse).ToList());
			#region 注释
			//var dat = DateTime.Now.Date;
			//var sql = $"select warehouseid para,count(1) coun from scb_Xsjl(nolock) where logicswayoid =144 and state = 0 and temp3= '0'  and country ='us' and temp10 = 'us' and newdateis > '{dat.ToString("yyyy-MM-dd")}' and (filterflag >5 or (filterflag =4 and ISNULL(trackno,'') <>'')) group by warehouseid ";
			//var matchdata = db.Database
			//	.SqlQuery<GroupByCountDto>(sql)
			//	.ToList();

			//if (!matchdata.Any(p => p.para == 99) || matchdata.Any(p => p.para == 99 && p.coun <= 1700))
			//{
			//	cansendWare += "US16,";
			//}
			//if (!matchdata.Any(p => p.para == 24) || matchdata.Any(p => p.para == 24 && p.coun <= 600))
			//{
			//	cansendWare += "US06,";
			//}
			//if (!matchdata.Any(p => p.para == 44) || matchdata.Any(p => p.para == 44 && p.coun <= 800))
			//{
			//	cansendWare += "US08,";
			//}
			//var ontrac_us15time = new DateTime(2025, 8, 20, 4, 0, 0);
			//if (AMESTime.AMESNowUSA > ontrac_us15time && !matchdata.Any(p => p.para == 303) || matchdata.Any(p => p.para == 303 && p.coun <= 1000))
			//{
			//	cansendWare += "US15,";
			//}

			////if (!matchdata.Any(p => p.para == 82) || matchdata.Any(p => p.para == 82 && p.coun <= 600))
			////{
			////	cansendWare += "US11,";
			////}
			////20250714 武金凤 US11仓  单量无上限
			////20250912 武金凤 US11仓 最近AMAZONSHIPPING周末要改 怕仓库这边乱,设置下 2000 吧
			////cansendWare += "US11,";
			//if (!matchdata.Any(p => p.para == 82) || matchdata.Any(p => p.para == 82 && p.coun <= 1700))
			//{
			//	cansendWare += "US11,";
			//}
			#endregion

			if (string.IsNullOrEmpty(cansendWare))
			{
				canSend = false;
			}
			else
			{
				canSend = true;
			}
			#endregion

			return new Tuple<bool, string, string>(canSend, message, cansendWare.TrimEnd(','));
		}
		public Tuple<string, Dictionary<string, List<scb_logisticsForproduct_us_new>>> GetGOFOFee(MatchModel order, List<scb_WMS_MatchRule> MatchRules, List<LogisticsWareRelationModel> logisticsmodes, List<ScbckModel> EffectiveWarehouses, List<CpbhSizeInfo> cpRealSizeinfos)
		{
			var db = new cxtradeModel();
			var result = new Dictionary<string, List<scb_logisticsForproduct_us_new>>();
			//判断店铺平台
			var platformConfine = MatchRules.FirstOrDefault(p => p.Type == "GOFOXsptConfine"); //order.details.First().platform;
			var usefulPlatforms = db.scb_WMS_MatchRule_Details.Where(p => p.RID == platformConfine.ID).ToList();
			if ((!usefulPlatforms.Any(p => p.Type == "platform" && p.Parameter.ToLower() == order.xspt.ToLower()) && !usefulPlatforms.Any(p => p.Type == "Store" && p.Parameter.ToLower() == order.dianpu.ToLower())))
			{
				return new Tuple<string, Dictionary<string, List<scb_logisticsForproduct_us_new>>>($"店铺或平台不支持", result);
			}
			var message = string.Empty;

			var cansendZip_query = _cacheGOFOZip.Where(p => order.zip.StartsWith(p.DestZip) && p.State == order.statsa && p.IsValid == 1);
			var usefulWarehouses = logisticsmodes.Where(p => p.logisticsName == "GOFO").Select(p => p.warehouseIds).FirstOrDefault();
			if (!string.IsNullOrEmpty(usefulWarehouses))
			{
				cansendZip_query = cansendZip_query.Where(p => usefulWarehouses.Contains(p.OriginWarehouse));
			}
			var cansendZip = cansendZip_query.ToList();

			if (cansendZip.Count == 0)
			{
				return new Tuple<string, Dictionary<string, List<scb_logisticsForproduct_us_new>>>($"zip：{order.zip} 不支持", result);
			}

			var cpbh = order.details.First().cpbh;

			var realsizeinfo = cpRealSizeinfos.Where(p => p.cpbh == cpbh).FirstOrDefault();
			var weight_vol = realsizeinfo.bzcd * realsizeinfo.bzkd * realsizeinfo.bzgd / 166;
			var weight_phy = weight_vol > realsizeinfo.weight ? weight_vol : realsizeinfo.weight;
			var canGroud = true;
			var canParcel = true;
			if (weight_phy <= 20 && realsizeinfo.bzcd <= 23.62m && realsizeinfo.bzcd + realsizeinfo.bzkd + realsizeinfo.bzgd <= 59.06m)
			{
				canParcel = true;
			}
			else
			{
				canParcel = false;
				message += $"GOFO_Parcel 尺寸不符合;";
			}

			var logisticsmode_FeeList = db.scb_logisticsForproduct_us_new
				.Where(p => p.IsChecked == 1 && p.goods == cpbh && p.LogisticsType.StartsWith("GOFO")).OrderBy(p => p.fee).ToList();

			foreach (var effectiveWarehouse in EffectiveWarehouses)
			{
				if (result.ContainsKey(effectiveWarehouse.id))
				{
					continue;
				}
				var feelist = new List<scb_logisticsForproduct_us_new>();
				if (canGroud)
				{
					var zone = cansendZip.Where(p => p.OriginWarehouse == effectiveWarehouse.id && p.ServiceType == "GOFO").FirstOrDefault();
					if (zone == null)
					{
						message += $"{effectiveWarehouse.id} GOFO_Ground zone is null;";
						//continue;
					}
					else
					{
						var logisticsmode_Fee = logisticsmode_FeeList.Where(p => p.zone == zone.GNDZone && p.LogisticsType == "GOFO").FirstOrDefault();
						if (logisticsmode_Fee == null)
						{
							message += $"仓库:{effectiveWarehouse.id} zone:{zone.GNDZone} GOFO_Ground未获取到运费";
							//continue;
						}
						else
						{
							feelist.Add(logisticsmode_Fee);
						}
					}
				}
				if (canParcel)
				{
					var zone = cansendZip.Where(p => p.OriginWarehouse == effectiveWarehouse.id && p.ServiceType == "GOFO_Parcel").FirstOrDefault();
					if (zone == null)
					{
						message += $"{effectiveWarehouse.id} GOFO_Parcel zone is null;";
						//continue;
					}
					else
					{
						var logisticsmode_Fee = logisticsmode_FeeList.Where(p => p.zone == zone.GNDZone && p.LogisticsType == "GOFO_Parcel").FirstOrDefault();
						if (logisticsmode_Fee == null)
						{
							message += $"仓库:{effectiveWarehouse.id} zone:{zone.GNDZone} GOFO_Parcel未获取到运费";
							//continue;
						}
						else
						{
							feelist.Add(logisticsmode_Fee);
						}
					}
				}

				if (feelist.Count > 0)
					result.Add(effectiveWarehouse.id, feelist);
			}

			return new Tuple<string, Dictionary<string, List<scb_logisticsForproduct_us_new>>>(message, result);
		}

		public Tuple<string, Dictionary<string, decimal>> GetFimileFee(MatchModel order, List<scb_WMS_MatchRule> MatchRules, List<LogisticsWareRelationModel> logisticsmodes, List<ScbckModel> EffectiveWarehouses)
		{
			var result = new Dictionary<string, decimal>();
			//return new Tuple<string, Dictionary<string, decimal>>($"未通知开启Fimile打单", result);

			var db = new cxtradeModel();
			//判断店铺平台
			var platformConfine = MatchRules.FirstOrDefault(p => p.Type == "FimileXsptConfine"); //order.details.First().platform;
			var usefulPlatforms = db.scb_WMS_MatchRule_Details.Where(p => p.RID == platformConfine.ID).ToList();
			if ((!usefulPlatforms.Any(p => p.Type == "platform" && p.Parameter.ToLower() == order.xspt.ToLower()) && !usefulPlatforms.Any(p => p.Type == "Store" && p.Parameter.ToLower() == order.dianpu.ToLower())))
			{
				return new Tuple<string, Dictionary<string, decimal>>($"店铺或平台不支持", result);
			}
			var message = string.Empty;

			var cansendZip_query = _cacheFimileZip.Where(p => order.zip.StartsWith(p.DestZip) && p.State == order.statsa && p.IsValid == 1);
			var cansendZip = cansendZip_query.ToList();
			if (cansendZip.Count == 0)
			{
				return new Tuple<string, Dictionary<string, decimal>>($"zip：{order.zip} 不支持", result);
			}

			var usefulWarehouses = logisticsmodes.Where(p => p.logisticsName == "Fimile").Select(p => p.warehouseIds).FirstOrDefault();
			var cpbh = order.details.First().cpbh;
			var logisticsmode_FeeList = db.scb_logisticsForproduct_us_new
				.Where(p => p.IsChecked == 1 && p.goods == cpbh && p.LogisticsType == "Fimile").OrderBy(p => p.fee).ToList();

			foreach (var effectiveWarehouse in EffectiveWarehouses)
			{
				if (!usefulWarehouses.Contains(effectiveWarehouse.id))
				{
					message += $"{effectiveWarehouse.id} 仓库不支持;";
					continue;
				}
				if (result.ContainsKey(effectiveWarehouse.id))
				{
					continue;
				}
				var zone = cansendZip.Where(p => p.Origin == effectiveWarehouse.Origin).FirstOrDefault();
				if (zone == null)
				{
					message += $"{effectiveWarehouse.id} zone is null;";
					continue;
				}
				var logisticsmode_Fee = logisticsmode_FeeList.Where(p => p.zone == zone.GNDZone).FirstOrDefault();
				if (logisticsmode_Fee == null)
				{
					message += $"仓库:{effectiveWarehouse.id} zone:{zone.GNDZone} 未获取到运费";
					continue;
				}
				result.Add(effectiveWarehouse.id, logisticsmode_Fee.fee.Value);
			}

			return new Tuple<string, Dictionary<string, decimal>>(message, result);
		}


		public Tuple<string, Dictionary<string, decimal>> GetWestnovoFee(MatchModel order, List<scb_WMS_MatchRule> MatchRules, List<LogisticsWareRelationModel> logisticsmodes, List<ScbckModel> EffectiveWarehouses)
		{
			var result = new Dictionary<string, decimal>();

			//20251009 蒋建涛 华送新的物流规则，请更新，下周一开始恢复发货
			if (DateTime.Now < new DateTime(2025, 10, 13, 0, 0, 0))
			{
				return new Tuple<string, Dictionary<string, decimal>>($"2025-09-09蒋建涛通知暂不发WESTERN_POST", result);
			}

			var db = new cxtradeModel();

			//判断是否可发仓
			var usefulWarehouses = logisticsmodes.Where(p => p.logisticsName == "WESTERN_POST").Select(p => p.warehouseIds).FirstOrDefault();
			if (!string.IsNullOrEmpty(usefulWarehouses))
			{
				EffectiveWarehouses = EffectiveWarehouses.Where(p => usefulWarehouses.Contains(p.id)).ToList();
			}
			if (EffectiveWarehouses.Count == 0)
			{
				return new Tuple<string, Dictionary<string, decimal>>($"可发仓库:{usefulWarehouses},无可用库存", result);
			}
			//判断店铺平台
			var platformConfine = MatchRules.FirstOrDefault(p => p.Type == "WESTERNXsptConfine"); //order.details.First().platform;
			var usefulPlatforms = db.scb_WMS_MatchRule_Details.Where(p => p.RID == platformConfine.ID).ToList();
			if (!usefulPlatforms.Any(p => p.Type == "platform" && p.Parameter.ToLower() == order.xspt.ToLower()))
			{
				return new Tuple<string, Dictionary<string, decimal>>($"店铺或平台不支持", result);
			}

			#region 获取真实尺寸信息
			var sheet = order.details.First();

			//真实尺寸，向上取整
			var realSize = db.scb_realsize.Where(p => p.cpbh == sheet.cpbh && p.country == order.country).FirstOrDefault();
			var size_real = new List<decimal>();
			if (realSize == null)
			{
				var good = db.N_goods.Where(p => p.cpbh == sheet.cpbh && p.country == order.country).FirstOrDefault();
				size_real = new List<decimal>() { (decimal)good.bzcd, (decimal)good.bzkd, (decimal)good.bzgd };
			}
			else
			{
				size_real = new List<decimal>() { (decimal)realSize.bzcd, (decimal)realSize.bzkd, (decimal)realSize.bzgd };
			}
			size_real.Sort();//从小到大排序
			var length = Math.Ceiling(size_real[2]);
			var width = Math.Ceiling(size_real[1]);
			var height = Math.Ceiling(size_real[0]);
			var girth = length + 2 * (width + height);
			var weight_volume = length * width * height / 250;//体积重
			var message = $"货号:{sheet.cpbh};length:{length},width:{width},height:{height};";

			#endregion

			foreach (var effectiveWarehouse in EffectiveWarehouses)
			{
				try
				{
					var Westernstzone = _cacheWesternPostZip.Where(p => order.zip.StartsWith(p.DestZip) && p.OriginWarehouse == effectiveWarehouse.id && p.IsValid == 1).FirstOrDefault();
					//先判断仓库的zone对应邮编能发不
					if (Westernstzone == null)
					{
						throw new Exception($"邮编:{order.zip},GNDZone:{effectiveWarehouse.GNDZone} 不支持;");
					}
					//再获取运费
					var fee = GetWestnovoPieceFee((int)effectiveWarehouse.GNDZone, length, width, height, girth, weight_volume, ref message);

					result.Add(effectiveWarehouse.id, fee);

				}
				catch (Exception ex)
				{
					message += effectiveWarehouse.id + ex.Message;
				}
			}
			return new Tuple<string, Dictionary<string, decimal>>(message, result);
		}

		/// <summary>
		/// 向上取整
		/// </summary>
		/// <param name="zone"></param>
		/// <param name="length"></param>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <param name="girth"></param>
		/// <param name="weight_volume"></param>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		public decimal GetWestnovoPieceFee(int zone, decimal length, decimal width, decimal height, decimal girth, decimal weight_volume, ref string message)
		{
			var zone2 = -1m;
			var zone4 = -1m;
			var zone3 = -1m;
			var zone5 = -1m;
			var zone6 = -1m;
			#region  可以放表里，各个费用明细
			if (weight_volume <= 60)
			{
				zone2 = 17;
				zone3 = 19;
				zone4 = 22;
				zone5 = 25;
				zone6 = 27;
			}
			else if (weight_volume > 60 && weight_volume <= 70)
			{
				zone2 = 18;
				zone3 = 22;
				zone4 = 23;
				zone5 = 26;
				zone6 = 30;
			}
			else if (weight_volume > 70 && weight_volume <= 80)
			{
				zone2 = 21;
				zone3 = 24;
				zone4 = 25;
				zone5 = 29;
				zone6 = 32;
			}
			else if (weight_volume > 80 && weight_volume <= 90)
			{
				zone2 = 25;
				zone3 = 26;
				zone4 = 29;
				zone5 = 31;
				zone6 = 34;
			}
			else if (weight_volume > 90 && weight_volume <= 100)
			{
				zone2 = 27;
				zone3 = 29;
				zone4 = 31;
				zone5 = 32;
				zone6 = 37;
			}
			else if (weight_volume > 100 && weight_volume <= 110)
			{
				zone2 = 31;
				zone3 = 31;
				zone4 = 33;
				zone5 = 34;
				zone6 = 39;
			}
			else if (weight_volume > 110 && weight_volume <= 120)
			{
				zone2 = 33;
				zone3 = 34;
				zone4 = 35;
				zone5 = 37;
				zone6 = 40;
			}
			else if (weight_volume > 120 && weight_volume <= 130)
			{
				zone2 = 34;
				zone3 = 35;
				zone4 = 38;
				zone5 = 39;
				zone6 = 42;
			}
			else if (weight_volume > 130 && weight_volume <= 140)
			{
				zone2 = 38;
				zone3 = 38;
				zone4 = 40;
				zone5 = 41;
				zone6 = 45;
			}
			else if (weight_volume > 140 && weight_volume <= 150)
			{
				zone2 = 39;
				zone3 = 40;
				zone4 = 42;
				zone5 = 45;
				zone6 = 47;
			}
			else
			{
				throw new Exception($"不支持的重量{weight_volume}");
			}
			#endregion

			var baseFee = -1m;
			if (zone == 2)
			{
				baseFee = zone2;
			}
			else if (zone == 3)
			{
				baseFee = zone3;
			}
			else if (zone == 4)
			{
				baseFee = zone4;
			}
			else if (zone == 5)
			{
				baseFee = zone5;
			}
			else if (zone == 6)
			{
				baseFee = zone6;
			}
			else
			{
				throw new Exception($"不支持的zone:{zone}");
			}

			message += $"全包价:{baseFee}";
			var surchargeFee = 0m;
			if (length > 96 || girth > 130)
			{
				surchargeFee = 20m;
				message += $"附加费:{surchargeFee}";
			}
			message += $" (全包价+附加费)*0.8 ";
			var fee = (baseFee + surchargeFee) * 0.8m;
			return fee;
		}

		private bool CanSendAMZN(List<string> cklistForAMZN, List<scb_WMS_MatchRule> MatchRules, string CheckWarehousename, MatchModel order, string Tocountry, bool CanCAToUS, double weightReal, double weight, double? volNum2)
		{
			var cansendAmzn = false;
			#region 判断下能发AMZN不  如果包含amzn可发仓，就判断能发amzn不
			if (!order.ignoreAMZN && cklistForAMZN.Any(p => p == CheckWarehousename) && Tocountry == "US" && order.details.Sum(p => p.sl) == 1  //&& !order.dianpu.Contains("Prime")
																																				//&& !(CanCAToUS && Tocountry == "US" && (CheckWarehousename == "US21" || CheckWarehousename == "US121")
				&& IsAMZNXCpbh(order.dianpu, order.details, MatchRules)
				&& IsAMZNXsptOrDianpu(MatchRules, order.xspt, order.dianpu, order.email)
				)
			{
				var girth = 0m;
				var length = 0m;
				var wight = 0m;
				var height = 0m;
				var applyWeightForAMZN = weight;
				var realWeightForAMZN = weightReal;

				var girth_real = 0m;
				var length_real = 0m;
				var wight_real = 0m;
				var height_real = 0m;

				var db = new cxtradeModel();
				var cpbh = order.details.First().cpbh;
				var logisticsApply_Size = db.Scb_LogisticsApply_Size.FirstOrDefault(p => p.Country == order.country && p.LogisticsName == "AMZN" && p.Cpbh == cpbh);
				if (logisticsApply_Size != null)
				{
					length = logisticsApply_Size.Length;
					wight = logisticsApply_Size.Width;
					height = logisticsApply_Size.Heigth;
					applyWeightForAMZN = (double)logisticsApply_Size.Weight;
					realWeightForAMZN = (double)logisticsApply_Size.Weight;
				}
				else
				{
					var good = db.N_goods.FirstOrDefault(p => p.cpbh == cpbh && p.country == "US");
					length = (decimal)good.bzcd;
					wight = (decimal)good.bzkd;
					height = (decimal)good.bzgd;
					var vol_weight = (double)volNum2 / 200;
					//applyWeightForAMZN = vol_weight > weight ? vol_weight : weight;
				}

				#region  计算真实计费重
				var realsize = db.scb_realsize.FirstOrDefault(p => p.cpbh == cpbh && p.country == "US");
				if (realsize != null)
				{
					length_real = (decimal)realsize.bzcd;
					wight_real = (decimal)realsize.bzkd;
					height_real = (decimal)realsize.bzgd;
					//var vol_weightReal = (double)realsize.bzcd * (double)realsize.bzkd * (double)realsize.bzgd / 200;//体积重
					//realWeightForAMZN = vol_weightReal > weightReal ? vol_weightReal : weightReal;
				}
				else
				{
					length_real = length;
					wight_real = wight;
					height_real = height;
				}
				#endregion

				girth_real = length_real + (wight_real + height_real) * 2;
				girth = length + (wight + height) * 2;
				//20250919 武金凤 打单重不超过50lbs，且实际尺寸-长不超过37，且实际尺寸-宽不超过30，且实际尺寸-高不超过24，且打单尺寸-周长不超过105
				if (Math.Ceiling(girth) > 0 && Math.Ceiling(length_real) <= 37 && Math.Ceiling(wight_real) < 30 && Math.Ceiling(height_real) < 24 && Math.Ceiling(girth) <= 105 && Math.Ceiling(applyWeightForAMZN) <= 50 && Math.Ceiling(applyWeightForAMZN) > 1)
				{
					scbck ck = null;
					ck = db.scbck.FirstOrDefault(p => p.id == CheckWarehousename);
					var upszip = _cacheZip.FirstOrDefault(p => p.Origin == ck.Origin && order.zip.StartsWith(p.DestZip));
					if (ck != null && upszip != null
						 && ((Math.Ceiling(applyWeightForAMZN) > 5 && Math.Ceiling(applyWeightForAMZN) <= 10 && upszip.GNDZone <= 8) || (Math.Ceiling(applyWeightForAMZN) > 10 && Math.Ceiling(applyWeightForAMZN) <= 50 && upszip.GNDZone <= 6)
						|| (Math.Ceiling(realWeightForAMZN) > 1 && Math.Ceiling(realWeightForAMZN) <= 5 && upszip.GNDZone <= 8)))
					{
						cansendAmzn = true;
					}

				}
			}
			#endregion
			return cansendAmzn;
		}

		public bool BlackAddress(string dianpu, string pingtai, string khname, string address1, string address2, string city, string tocountry, string email, string fkzh, string phone, string bz)
		{
			var BlackList5 = new List<string>()
			{
				"vendor","homedepot","target","lowes"
			};
			var BlackList6 = new List<string>()
			{
				"FDSUS-BP","QUIFLY-HD","Costway-SLHD","DIXON-HD","SKONYON-HD","TOPIA-HD","DELPHI-HD","COSTWAY-PHILIPWE","COSTWAY-HANONE"
			};
			// 先做前置过滤
			if (khname?.Contains("wayfair") == true ||
				BlackList5.Contains(pingtai?.ToLower() ?? "") ||
				BlackList6.Contains(dianpu?.ToUpper() ?? ""))
			{
				return false;
			}
			if (!khname.Contains("wayfair") && !BlackList5.Contains(pingtai.ToLower()) && !BlackList6.Contains(dianpu.ToUpper()) && !bz.Contains("营销"))
			{
				string sql = @"
        SELECT COUNT(*)
        FROM [PimOut]..[bl_addr_ord] ord
        INNER JOIN [PimOut]..[bl_addr] addr 
            ON addr.adress = ord.adress 
            AND addr.cus_name = ord.cus_name
            AND addr.city = ord.city 
            AND addr.tocountry = ord.tocountry 
        WHERE ord.country = @country
          AND addr.refund_num >= 2 
          AND (
                -- 地址匹配（始终检查）
                (ord.cus_name = @khname 
                 AND ord.city = @city 
                 AND (ord.adress = @adress OR ord.adress = @address1)
                )
                --OR
                    -- 账户匹配（仅当双方字段都非空时启用）
                    --(
                            -- email 匹配（仅当双方都有效）
                    --(
                        --NULLIF(LTRIM(RTRIM(ord.email)), '') IS NOT NULL
                        --AND NULLIF(LTRIM(RTRIM(@email)), '') IS NOT NULL
                        --AND ord.email = @email
                    --)
                    --OR
                    -- account_id 匹配（仅当双方都有效）
                    --(
                        --NULLIF(LTRIM(RTRIM(ord.account_id)), '') IS NOT NULL
                        --AND NULLIF(LTRIM(RTRIM(@fkzh)), '') IS NOT NULL
                        --AND ord.account_id = @fkzh
                    --)
                    --OR
                    -- phone 匹配（仅当双方都有效）
                    --(
                        --NULLIF(LTRIM(RTRIM(ord.phone)), '') IS NOT NULL
                        --AND NULLIF(LTRIM(RTRIM(@phone)), '') IS NOT NULL
                        --AND ord.phone = @phone
                    --)
                --)
              )";

				// 安全处理 null 值：传 null 而不是字符串 "null"
				var parameters = new[]
				{
					new SqlParameter("@country", tocountry ?? (object)DBNull.Value),
					new SqlParameter("@khname", khname ?? (object)DBNull.Value),
					new SqlParameter("@city", city ?? (object)DBNull.Value),
					new SqlParameter("@adress", address1 ?? (object)DBNull.Value),
					new SqlParameter("@address1", address2 ?? (object)DBNull.Value),
					new SqlParameter("@email", email ?? (object)DBNull.Value),
					new SqlParameter("@fkzh", fkzh ?? (object)DBNull.Value),
					new SqlParameter("@phone", phone ?? (object)DBNull.Value)
				};
				var db = new cxtradeModel();
				int count = db.Database.SqlQuery<int>(sql, parameters).FirstOrDefault();
				if (count >= 1)
				{
					return true;
				}
				else
				{
					return false;
				}
			}
			return false;
		}
		#endregion
		#region 辅助

		public class GoodsModel
		{
			public string cpbh { get; set; }
			public int qty { get; set; }
			public decimal Length { get; set; }
			public decimal Width { get; set; }
			public decimal Height { get; set; }
			/// <summary>
			/// 计费重
			/// </summary>
			public decimal CalW { get; set; }
			/// <summary>
			/// 实重
			/// </summary>
			public decimal Weight { get; set; }

			public decimal bzcd { get; set; }
			public decimal bzkd { get; set; }
			public decimal bzgd { get; set; }
			public decimal weight_g { get; set; }
		}

		public class GoodsTheoryFee
		{
			public string cpbh { get; set; }
			public decimal fee { get; set; }
		}

		public class WarehouseDetail
		{
			/// <summary>
			/// 仓库编号
			/// </summary>
			public string WarehouseNo { set; get; }
			/// <summary>
			/// 有效库存
			/// </summary>
			public int EffectiveInventory { set; get; }
			/// <summary>
			/// fds库存保障货号
			/// </summary>
			public int FDS_StockItemNo { set; get; }
			/// <summary>
			/// fds库存保障数量
			/// </summary>
			public int FDS_Stock { set; get; }
			/// <summary>
			/// 实际库存
			/// </summary>
			public int kcsl { set; get; }
			/// <summary>
			/// 当前占用
			/// </summary>
			public int tempkcsl { set; get; }
			/// <summary>
			/// 过期占用
			/// </summary>
			public int unshipped { set; get; }
			/// <summary>
			/// WMS占用
			/// </summary>
			public int occupation { set; get; }

		}

		private class WarehouseDetailByTruck
		{
			/// <summary>
			/// 仓库编号
			/// </summary>
			public string WarehouseNo { set; get; }
			/// <summary>
			/// 有效库存
			/// </summary>
			public int EffectiveInventory { set; get; }
			/// <summary>
			/// 当前发货数量
			/// </summary>
			public int Qty { set; get; }
			/// <summary>
			/// 当前发货货号
			/// </summary>
			public string ItemNo { set; get; }
		}
		private class WarehouseKCSLDetail
		{
			/// <summary>
			/// 仓库编号
			/// </summary>
			public string WarehouseNo { set; get; }
			/// <summary>
			/// 当前发货数量
			/// </summary>
			public int Qty { set; get; }
		}

		private class WarehouseLogisticFee
		{
			public ScbckModel ck { get; set; }
			public decimal? fee { get; set; }
			public int? LogisticsID { get; set; }
			public string LogisticsType { get; set; }
			public decimal? totalfee { get; set; }

		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="arrSort"></param>
		/// <param name="maxValue"></param>
		/// <param name="secondValue"></param>
		public void GetMaxMiddValue(double[] arrSort, out double maxValue, out double secondValue)
		{
			maxValue = 0;
			secondValue = 0;
			double temp = 0;
			for (int i = 0; i < arrSort.Length; i++)
			{
				for (int j = i + 1; j < arrSort.Length; j++)
				{
					if (arrSort[j] >= arrSort[i])
					{
						temp = arrSort[j];
						arrSort[j] = arrSort[i];
						arrSort[i] = temp;
					}
				}
			}
			maxValue = arrSort[0];
			secondValue = arrSort[1];
		}

		private bool CheckByGB(scb_xsjl Order, ref OrderMatchResult OrderMatch)
		{
			////退件单
			//if (Order.temp3 == "7")
			//{
			//	OrderMatch.Result.Message = string.Format("订单编号:{0}:退件单请手动派单", Order.number);
			//	return false;
			//}
			var db = new cxtradeModel();
			var postcode = db.scb_postcode_eu_islands.Where(a => a.postcode.ToLower() == Order.zip.ToLower() && a.country == "GB").FirstOrDefault();
			//异常地址
			if (Order.zip.ToUpper().StartsWith("JE") || Order.zip.ToUpper().StartsWith("GY") || Order.zip.ToUpper().StartsWith("GE")
				|| Order.zip.ToUpper().StartsWith("IM") || Order.zip.ToUpper().StartsWith("HS") || Order.zip.ToUpper().StartsWith("ZE"))
			{
				OrderMatch.Result.Message = string.Format("订单编号:{0} JE、GY、GE、IM、HS、ZE 开头的邮编地址请手动派单，偏远地区尽量别发！", Order.number);
				return false;
			}
			else if (postcode != null)
			{
				OrderMatch.Result.Message = string.Format("订单编号:{0} {1}邮编地址请手动派单，偏远地区尽量别发！", Order.number, Order.zip);
				return false;
			}
			return true;
		}

		/// <summary>
		/// 英国小岛邮编
		/// </summary>
		/// <returns></returns>
		private List<string> GetIsletZipGB()
		{
			List<string> list = new List<string> { "IV21 2DB",
"KW15 1AT",
"KW15 1BJ",
"KW15 1BP",
"KW15 1FJ",
"KW15 1JE",
"KW15 1LT",
"KW15 1PL",
"KW15 1QG",
"KW15 1SR",
"KW15 1SZ",
"KW15 1TG",
"KW15 1TN",
"KW15 1UQ",
"KW15 1UW",
"KW15 1UX",
"KW15 1WB",
"KW15 1XJ",
"KW15 1XQ",
"KW15 1YW",
"KW15 1ZU",
"KW16 3BG",
"KW16 3BY",
"KW16 3EP",
"KW16 3EQ",
"KW16 3EY",
"KW16 3HD",
"KW16 3LL",
"KW16 3NY",
"KW17 2AN",
"KW17 2AP",
"KW17 2BA",
"KW17 2BL",
"KW17 2DH",
"KW17 2DN",
"KW17 2ES",
"KW17 2ET",
"KW17 2HS",
"KW17 2HT",
"KW17 2HW",
"KW17 2HZ",
"KW17 2LQ",
"KW17 2LT",
"KW17 2NH",
"KW17 2QH",
"KW17 2QJ",
"KW17 2RE",
"KW17 2RN",
"KW17 2RP",
"KW17 2RS",
"KW17 2RU",
"KW17 2RZ",
"KW17 2SB",
"KW17 2SD",
"KW17 2SP",
"KW17 2SS",
"KW17 2SW",
"KW17 2TJ",
"KW17 2TN",
"KW17 2UF",
"PA41 7AA",
"PA41 7AD",
"PA42 7BL",
"PA42 7EP",
"PA43 7AB",
"PA43 7AE",
"PA43 7JS",
"PA44 7PB",
"PA46 7RL",
"PA47 7SY",
"PA48 7TN",
"PA48 7UD",
"PA49 7UN",
"PA60 7AB",
"PA60 7XX",
"PA65 6AY",
"PA65 6BA",
"PA67 6DG",
"PA71 6HR",
"PA72 6JB",
"PA72 6JS",
"PA74 6NH",
"PA75 6AD",
"PA75 6NR",
"PA75 6NT",
"PA75 6PS",
"PA75 6PY",
"PA75 6QA",
"PA75 6RA",
"PA76 6SJ",
"PA77 6TR",
"PA77 6TW",
"PA77 6TW",
"PA77 6UA",
"PA77 6UH",
"PA78 6SY"};
			list.ForEach(p =>
			{
				p = p.ToUpper();
			});
			return list;
		}

		private string GetUSServceType(int logisticsId, string logisticsname = "")
		{
			var servicetype = logisticsname;
			switch (logisticsId)
			{
				case 3:
					servicetype = "First|Default|Parcel|OFF"; break;
				case 4:
					servicetype = "UPSGround"; break;
				case 18:
					servicetype = "FEDEX_GROUND"; break;
				case 24:
					servicetype = "UPSSurePost"; break;
				case 80:
					servicetype = "UPSToCA"; break;
				case 78:
					servicetype = "FedexToCA"; break;
				case 48:
					servicetype = "UPSInternational"; break;
				case 84:
					servicetype = "Estafeta"; break;
				case 79:
					servicetype = "FedexToUS"; break;
				default:
					servicetype = logisticsname; break;
			}
			if (string.IsNullOrEmpty(servicetype))
			{
				throw new Exception("未找到符合的物流服务");
			}
			return servicetype;
		}

		#endregion

		#region 测试部分
		[Route("Test/US/Order")]
		[HttpGet]
		public OrderMatchResult Test_Order(decimal number)
		{
			return BaseOrder(number, "");
		}
		/// <summary>
		/// EU
		/// </summary>
		/// <param name="number">订单主键</param>
		/// <returns></returns>
		[HttpGet, Route("Test/EU/Order"), Route("Test/DE/Order")]
		public OrderMatchResult Test_OrderByEU(decimal number)
		{
			return BaseOrderByEU(number);
		}
		[HttpGet, Route("LogisticsRule")]
		public string LogisticsRule(string ItemNo)
		{
			var OrderMatch = new OrderMatchResult();
			var db = new cxtradeModel();
			var good = db.N_goods.Where(g => g.cpbh == ItemNo && g.country == "US" && g.groupflag == 0).FirstOrDefault();
			var weight = good.weight_express / 1000 * 2.2046226;
			var sizes = new List<double?>() { good.bzgd, good.bzcd, good.bzkd };
			var maxNum = sizes.Max();
			var secNum = sizes.Where(f => f < maxNum).Max();
			if (sizes.Count(f => f < maxNum) <= 1)
				secNum = maxNum;
			if (weight < 1)
			{
				//OrderMatch.Logicswayoid = 24;
				//OrderMatch.SericeType = "First|Default|Parcel|OFF";
			}
			else if (1 <= weight && weight < 8 && maxNum < 34 && secNum < 17)
			{
				var ups_weight = good.bzcd * good.bzgd * good.bzkd / 300;
				var fedex_weight = good.bzcd * good.bzgd * good.bzkd / 194;
				if (ups_weight > good.weight_express && fedex_weight > good.weight_express && ups_weight < 8 && fedex_weight < 8)
				{
					OrderMatch.Logicswayoid = 24;
					OrderMatch.SericeType = "UPSSurePost";
				}
				////暂时禁用SMART_POST
				//if (ups_weight < good.weight && fedex_weight < good.weight)
				//{
				//    OrderMatch.Logicswayoid = 21;
				//    OrderMatch.SericeType = "SMART_POST";
				//}
				if (1 < ups_weight && ups_weight < 8 && fedex_weight > 8)
				{
					OrderMatch.Logicswayoid = 24;
					OrderMatch.SericeType = "UPSSurePost";
				}
			}
			else if (weight >= 50)
			{
				OrderMatch.Logicswayoid = 18;
				OrderMatch.SericeType = "FEDEX_GROUND";
			}
			else if (weight < 50)
			{
				var volNum = good.bzcd + 2 * (good.bzkd + good.bzgd);
				if ((47 < maxNum && maxNum < 108) || (secNum > 29) || (121 <= volNum && volNum < 165))
				{
					OrderMatch.Logicswayoid = 18;
					OrderMatch.SericeType = "FEDEX_GROUND";
				}
			}
			return string.Format("server:{0} {1}*{2}*{3} weight:{4} ups:{5} fedex:{6} ",
				OrderMatch.SericeType, good.bzcd, good.bzkd, good.bzgd, weight, good.bzcd * good.bzgd * good.bzkd / 300, good.bzcd * good.bzgd * good.bzkd / 194);
		}
		#endregion
	}

	public class EUMatchModel
	{
		public decimal number { get; set; }
		public string OrderID { get; set; }
		public int? filterflag { get; set; }
		public int state { get; set; }
		public string lywl { get; set; }
		public string temp3 { get; set; }
		public int? zsl { get; set; }
		public int? warehouseid { get; set; }
		public string temp10 { get; set; }
		public string dianpu { get; set; }
		public string xspt { get; set; }
		public string chck { get; set; }

		#region 客户信息
		public string email { get; set; }
		public string zip { get; set; }
		public string adress { get; set; }
		public string address1 { get; set; }
		public string city { get; set; }
		public string statsa { get; set; }
		public string khname { get; set; }
		public string phone { get; set; }
		public string fkzh { get; set; }
		public string bz { get; set; }
		#endregion

		public List<EUMatchSheetModel> sheets { get; set; } = new List<EUMatchSheetModel>();
	}

	public class EUMatchSheetModel
	{
		public decimal number { get; set; }
		public decimal? father { get; set; }
		public string cpbh { get; set; }
		public string sku { get; set; }
		public string itemno { get; set; }
		public int sl { get; set; }
	}

	public class MatchFeeResultModel
	{
		public int logicswayoid { get; set; }
		public string serviceType { get; set; }
		public string message { get; set; }
		public decimal matchfee { get; set; } = 0;
		public decimal other { get; set; }
		public bool state { get; set; } = true;
	}
	public class GroupByCountDto
	{
		public int para { get; set; }
		public int coun { get; set; }
	}

}