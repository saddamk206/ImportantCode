using IMA.CustomModel;
using IMA.Helperclass;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Client.Services;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;
using IMA;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using StudyBrightApp;
using RestSharp;
using System.Linq;
using IMA.Models;
using static IMA.Controllers.SagePayController;

namespace IMA.Controllers
{
    [RoutePrefix("api/SagePay")]
    public class SagePayController : ApiController
    {

        private static Random random = new Random();

        Guid _contactId;
        CrmConnection connection = CrmConnection.Parse(ConfigurationManager.ConnectionStrings["CRMOnline"].ConnectionString);
        private static OrganizationService _getorgService;

        [Route("Payment")]
        public HttpResponseMessage Post(paymentData data)
        {
            try
            {
                var k = RandomString(6);
                string date = data.ExpireYear.Substring(2);
                CustomerTransaction customerTransaction = new CustomerTransaction();

                string base64Encoded = MerchantAPI.IntegrationKey + ":" + MerchantAPI.IntegrationPassword;
                byte[] toEncodeAsBytes = System.Text.ASCIIEncoding.ASCII.GetBytes(base64Encoded);
                string Token = System.Convert.ToBase64String(toEncodeAsBytes);
                MerchantAPIResult rsltdata = new MerchantAPIResult();
                var client = new RestClient("https://pi-test.sagepay.com/api/v1/merchant-session-keys");
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                var request = new RestRequest(Method.POST);
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("Authorization", "Basic " + Token);
                string rawdata = "{\r\n    \"vendorName\": \"" + SagePayCredentials.vendorName + "\"\r\n}";
                request.AddParameter("application/json", "{\r\n    \"vendorName\": \"" + SagePayCredentials.vendorName + "\"\r\n}", ParameterType.RequestBody);
                IRestResponse response = client.Execute(request);
                var discription = response.StatusDescription;
                string result = Convert.ToString(response.Content);
                rsltdata = JsonConvert.DeserializeObject<MerchantAPIResult>(result);

                //for card identifier **** STEP-2 *************
                CardIdentifierResult CIResult = new CardIdentifierResult();
                var client1 = new RestClient("https://pi-test.sagepay.com/api/v1/card-identifiers");
                var request1 = new RestRequest(Method.POST);
                cardDetailsList cdList = new cardDetailsList();
                CardDetails CD = new CardDetails();


                CD.cardholderName = data.NameOnCard;// ;
                CD.cardNumber = data.CardNumber;//
                CD.expiryDate = data.ExpireMonth + date;
                CD.securityCode = data.CardCvc;// "123";

                cdList.cardDetails = CD;
                var Data2Send = JsonConvert.SerializeObject(cdList);
                request1.AddHeader("content-type", "application/json");
                request1.AddHeader("authorization", "Bearer " + rsltdata.merchantSessionKey);
                request1.AddParameter("application/json", Data2Send, ParameterType.RequestBody);
                IRestResponse response1 = client1.Execute(request1);
                string result1 = Convert.ToString(response1.Content);
                CIResult = JsonConvert.DeserializeObject<CardIdentifierResult>(result1);
                var ststuscode = response1.StatusCode;
                //for transaction ****** sTEP 3 *****************
                OrderResult OrResult = new OrderResult();
                OrderData OD = new OrderData();
                OD.amount = Convert.ToInt32(Convert.ToDecimal(data.PaymentAmount));
                OD.apply3DSecure = "UseMSPSetting";
                OD.billingAddress = new BillingAddress();
                OD.billingAddress.address1 = data.billingaddress.address1;  // "407 St. John Street";
                OD.billingAddress.city = data.billingaddress.city;  // "London";
                                                                    // OD.billingAddress.country = data.shippingDetails.shippingCountry.Split('_')[0];
                OD.billingAddress.country = data.shippingDetails.shippingState;
                OD.billingAddress.postalCode = data.billingaddress.postalCode;  // "EC1V 4AB";
                OD.shippingDetails = new ShippingDetails();
                OD.shippingDetails.recipientFirstName = data.shippingDetails.recipientFirstName;
                OD.shippingDetails.recipientLastName = data.shippingDetails.recipientLastName;
                OD.shippingDetails.shippingAddress1 = data.shippingDetails.shippingAddress1;
                OD.shippingDetails.shippingAddress2 = data.shippingDetails.shippingAddress2;
                OD.shippingDetails.shippingCity = data.shippingDetails.shippingCity;
                //  OD.shippingDetails.shippingCountry = data.shippingDetails.shippingCountry.Split('_')[0];
                OD.shippingDetails.shippingCountry = data.shippingDetails.shippingState;
                OD.shippingDetails.shippingPostalCode = data.shippingDetails.shippingPostalCode;
                OD.shippingDetails.shippingState = data.shippingDetails.shippingState;
                OD.currency = "GBP";
                OD.description = "Test Payment";
                OD.customerFirstName = data.shippingDetails.recipientFirstName; // "Govind";
                OD.customerLastName = data.shippingDetails.recipientLastName; // "Chouksey";
                OD.entryMethod = "Ecommerce";
                OD.transactionType = "Payment";
                OD.vendorTxCode = "des" + k;// SagePayCredentials.vendorName;
                OD.paymentMethod = new PaymentMethod();
                OD.paymentMethod.card = new Card();
                OD.paymentMethod.card.cardIdentifier = CIResult.cardIdentifier;
                OD.paymentMethod.card.merchantSessionKey = rsltdata.merchantSessionKey;
                OD.paymentMethod.card.save = true;
                var Data2Send4Order = JsonConvert.SerializeObject(OD);
                var client2 = new RestClient("https://pi-test.sagepay.com/api/v1/transactions");
                var request2 = new RestRequest(Method.POST);
                request2.AddHeader("content-type", "application/json");
                request2.AddHeader("authorization", "Basic " + Token);
                request2.AddParameter("application/json", Data2Send4Order, ParameterType.RequestBody);
                IRestResponse response2 = client2.Execute(request2);
                string result2 = Convert.ToString(response2.Content);
                OrResult = JsonConvert.DeserializeObject<OrderResult>(result2);
                var PStatusCode = response2.StatusCode;
                var PStatusDescription = response2.StatusDescription;
                if (OrResult.status == "Ok")
                {
                    using (_getorgService = new OrganizationService(connection))
                    {
                        customerTransaction = JsonConvert.DeserializeObject<CustomerTransaction>(result2);
                        Entity my_paymenttran = new Entity("my_paymenttran");
                        my_paymenttran.Attributes["my_customercontact"] = new EntityReference("contact", new Guid(data.ContactId));
                        my_paymenttran["my_name"] = data.firstname + "_" + data.lastname + ":TransactionId_" + customerTransaction.transactionId;
                        my_paymenttran["my_transactionid"] = customerTransaction.transactionId;
                        my_paymenttran["my_transactiontype"] = customerTransaction.transactionType;
                        my_paymenttran["my_statusdetail"] = customerTransaction.statusDetail;
                        my_paymenttran["my_statuscode"] = customerTransaction.statusCode;
                        my_paymenttran["my_retrievalreference"] = customerTransaction.retrievalReference.ToString();
                        my_paymenttran["my_bankauthorisationcode"] = customerTransaction.bankAuthorisationCode;
                        my_paymenttran["my_bankresponsecode"] = customerTransaction.bankResponseCode;
                        my_paymenttran["my_cardtype"] = customerTransaction.paymentMethod.card.cardType;
                        my_paymenttran["my_expirydate"] = customerTransaction.paymentMethod.card.expiryDate;
                        my_paymenttran["my_lastfourdigits"] = customerTransaction.paymentMethod.card.lastFourDigits;
                        my_paymenttran["my_cardidentifier"] = customerTransaction.paymentMethod.card.cardIdentifier;
                        my_paymenttran["my_reusable"] = customerTransaction.paymentMethod.card.reusable.ToString();
                        if (customerTransaction.__invalid_name__3DSecure != null)
                        {
                            my_paymenttran["my_3ssecurestatus"] = customerTransaction.__invalid_name__3DSecure.status;
                        }
                        else
                        {
                            my_paymenttran["my_3ssecurestatus"] = "Notcheck";
                        }
                        my_paymenttran["my_status"] = customerTransaction.status;
                        //SAVE shipping addredd
                        my_paymenttran["my_recipientfirstname"] = data.shippingDetails.recipientFirstName;
                        my_paymenttran["my_recipientlastname"] = data.shippingDetails.recipientLastName;
                        my_paymenttran["my_shippingstate"] = data.shippingDetails.shippingAddress1;
                        my_paymenttran["my_shippingaddress1"] = data.shippingDetails.shippingAddress1;
                        my_paymenttran["my_shippingaddress2"] = data.shippingDetails.shippingAddress2;
                        my_paymenttran["my_shippingcity"] = data.shippingDetails.shippingCity;
                        my_paymenttran["my_shippingcountry"] = data.shippingDetails.shippingState;
                        my_paymenttran["my_shippingpostalcode"] = data.shippingDetails.shippingPostalCode;
                        Guid _paymenttranId = _getorgService.Create(my_paymenttran);
                        //create customrr order
                        if (_paymenttranId != null)
                        {
                            DBCrm db = new DBCrm();
                            var productlistid = db.GetCartProductPrice(_getorgService, data.item, data.Currency);
                            Entity my_customerorder = new Entity("my_customerorder");
                            my_customerorder.Attributes["my_orderedcontact"] = new EntityReference("contact", new Guid(data.ContactId));
                            my_customerorder.Attributes["my_name"] = data.shippingDetails.recipientFirstName + "_" + data.shippingDetails.recipientLastName;
                            my_customerorder.Attributes["my_transaction"] = new EntityReference("my_paymenttran", new Guid(_paymenttranId.ToString()));
                            my_customerorder.Attributes["my_currency"] = new EntityReference("transactioncurrency", new Guid(data.Currency));
                            my_customerorder.Attributes["my_paidamount"] = new Money(Convert.ToDecimal(OD.amount));
                            my_customerorder.Attributes["my_emailaddress"] = data.email;
                            my_customerorder.Attributes["my_carriercharges"] = data.deliveryCharge;
                            my_customerorder.Attributes["my_orderedcontact"] = new EntityReference("contact", new Guid(data.ContactId));
                            Guid _customerorderId = _getorgService.Create(my_customerorder);


                            foreach (var p in data.item)
                            {
                                foreach (var pro in productlistid)
                                {
                                    var isMember = false;
                                    if (pro.Pid == p.productid)
                                    {
                                        Entity my_ordersummary = new Entity("my_ordersummary");
                                        my_ordersummary.Attributes["my_ordersummeryid"] = new EntityReference("my_customerorder", new Guid(_customerorderId.ToString()));

                                        if (data.isMember.ToLower() == "true")
                                        {
                                            isMember = true;
                                            my_ordersummary.Attributes["my_totalprice"] = Convert.ToInt32(p.quantity) * Convert.ToDecimal(pro.MemberPrice);
                                        }
                                        else
                                        {
                                            my_ordersummary.Attributes["my_totalprice"] = Convert.ToInt32(p.quantity) * Convert.ToDecimal(pro.Price);
                                        }
                                        my_ordersummary.Attributes["my_currencyprice"] = new EntityReference("my_product_currencyandprice", new Guid(pro.Id));
                                        my_ordersummary.Attributes["my_currency"] = new EntityReference("transactioncurrency", new Guid(data.Currency));
                                        my_ordersummary.Attributes["my_quantity"] = Convert.ToInt32(p.quantity);
                                        my_ordersummary.Attributes["my_name"] = pro.Name;
                                        //my_ordersummary.Attributes["my_answer"] = data.prodQuestionAnswerList[0].Answer;

                                        //my_ordersummary.Attributes["my_answer"] = data.prodQuestionAnswerList[1].Answer;

                                        //my_ordersummary.Attributes["my_answer"] = data.prodQuestionAnswerList[2].Answer;
                                        Guid _my_ordersummaryid = _getorgService.Create(my_ordersummary);
                                        if (data.prodQuestionAnswerList != null)
                                        {
                                            foreach (var k1 in data.prodQuestionAnswerList)
                                            {
                                                Entity my_productanswer = new Entity("my_productanswer");
                                                my_productanswer.Attributes["my_ordersummary"] = new EntityReference("my_ordersummary", _my_ordersummaryid);
                                                my_productanswer.Attributes["my_question"] = new EntityReference("my_productquestion", new Guid(k1.QuestionId));
                                                my_productanswer.Attributes["my_name"] = k1.Answer;
                                                Guid my_productanswerId = _getorgService.Create(my_productanswer);
                                            }
                                        }
                                        //Update product Quantity
                                        var data12 = db.getSingleProduct(_getorgService, p.productid, data.Currency, isMember);
                                        Entity UpdateProductQty = new Entity("product");
                                        UpdateProductQty.Attributes["productid"] = new Guid(data12.productid);
                                        UpdateProductQty.Attributes["my_stocklevel"] = data12.StockLevel - Convert.ToInt32(p.quantity);
                                        _getorgService.Update(UpdateProductQty);
                                        data.statusCode = OrResult.statusCode;
                                        data.statusDetail = OrResult.statusDetail;
                                        data.transactionId = OrResult.transactionId;
                                        data.acsUrl = OrResult.acsUrl;
                                        data.paReq = OrResult.paReq;
                                        data.status = OrResult.status;
                                        bool status = postPaymentDetail(data);

                                    }
                                }
                            }
                            if (data.deliveryCharge != "")
                            {
                                Entity my_ordersummary = new Entity("my_ordersummary");
                                my_ordersummary.Attributes["my_ordersummeryid"] = new EntityReference("my_customerorder", new Guid(_customerorderId.ToString()));
                                my_ordersummary.Attributes["my_totalprice"] =Convert.ToDecimal(data.deliveryCharge);
                                my_ordersummary.Attributes["my_currency"] = new EntityReference("transactioncurrency", new Guid(data.Currency));
                                my_ordersummary.Attributes["my_quantity"] = 0;
                                my_ordersummary.Attributes["my_name"] = "Postage";
                                Guid _my_ordersummaryid = _getorgService.Create(my_ordersummary);
                            }
                            return Request.CreateResponse(HttpStatusCode.OK, customerTransaction);
                        }

                    }
                }
                else
                {
                    //data.statusCode = OrResult.statusCode;
                    //if (OrResult.statusDetail != null)
                    //{
                    //    customerTransaction.statusDetail = OrResult.statusDetail;
                    //}
                    //else
                    //{
                    //    customerTransaction.statusDetail = "Some Error occured..please check your card details!!!";
                    //}
                    customerTransaction.statusDetail = "Some Error occured..please check your card details!!!";
                    //data.transactionId = OrResult.transactionId;
                    //data.acsUrl = OrResult.acsUrl;
                    //data.paReq = OrResult.paReq;
                    //bool status = postPaymentDetail(data);
                    return Request.CreateResponse(HttpStatusCode.OK, customerTransaction.statusDetail);
                }

            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, ex.Message);
            }
            return Request.CreateResponse(HttpStatusCode.OK, "null");
        }

        [Route("getorderhistory")]
        public HttpResponseMessage Get(string cutomerid)
        {
            try
            {
                List<CustomerOrders> order = new List<CustomerOrders>();
                using (_getorgService = new OrganizationService(connection))
                {
                    DBCrm db = new DBCrm();
                    order = db.GetorderSummery(_getorgService, cutomerid);
                   
                    order.OrderBy(x => x.my_name);
                }


                return Request.CreateResponse(HttpStatusCode.OK, order);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, ex.Message);
            }
        }

        public bool postPaymentDetail(paymentData pdata)
        {
            try
            {
                using (_getorgService = new OrganizationService(connection))
                {

                    Entity newcontact = new Entity("my_paymentdetail");
                    newcontact["my_name"] = pdata.firstname + " " + pdata.lastname;
                    newcontact["my_cardholdername"] = pdata.NameOnCard;
                    newcontact["my_cardtype"] = pdata.cardtype;
                    newcontact["my_cardnumber"] = pdata.CardNumber;
                    newcontact["my_cardcvc"] = pdata.CardCvc;
                    newcontact["my_expirydate"] = pdata.ExpireMonth + pdata.ExpireYear;
                    newcontact["my_paymentamount"] = pdata.PaymentAmount;
                    newcontact["my_emailaddress"] = pdata.ContactId;//////////////
                    newcontact["my_mobilephone"] = pdata.phone;
                    newcontact["my_city"] = pdata.city;
                    newcontact["my_billingaddress"] = pdata.address1;
                    newcontact["my_billingaddress_line2"] = pdata.postalcode;
                    newcontact["my_statename"] = pdata.country;
                    newcontact.Attributes["my_currency"] = new EntityReference("transactioncurrency", new Guid(pdata.Currency));
                    newcontact.Attributes["my_contact"] = new EntityReference("contact", new Guid(pdata.ContactId));
                    newcontact["my_statuscode"] = pdata.statusCode;
                    newcontact["my_statusdetail"] = pdata.statusDetail;
                    newcontact["my_transactionid"] = pdata.transactionId;
                    newcontact["my_acsurl"] = pdata.acsUrl;
                    newcontact["my_pareq"] = pdata.paReq;
                    newcontact["my_status"] = pdata.status;
                    _contactId = _getorgService.Create(newcontact);
                    return true;
                }

                // return Request.CreateResponse(HttpStatusCode.Accepted, newuser);
            }
            catch (Exception ex)
            {
                return false;
                // return Request.CreateResponse(HttpStatusCode.BadRequest, "error");

            }
        }

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        public class MerchantAPI
        {
            public const string IntegrationKey = "aGkz4rfve3o3OgWPvIFCQmhBg2bWPAXfvQOG1ww9oPWUMydz9g";
            public const string IntegrationPassword = "2LJkK3RsNuvLJrIvevA32IWJHTRK587cjXfxsfjUZLU3DJ2qmBw5HkkYtaryGcVBU";
        }
        public class SagePayCredentials
        {
            public const string vendorName = "decisions";
            public const string userName = "decisions";
            public const string password = "TknfBrQw";
        }
        public class MerchantAPIResult
        {
            public string expiry { get; set; }
            public string merchantSessionKey { get; set; }
        }
        public class CardDetails
        {
            public string cardholderName { get; set; }
            public string cardNumber { get; set; }
            public string expiryDate { get; set; }
            public string securityCode { get; set; }
        }
        public class CardIdentifierResult
        {
            public string cardIdentifier { get; set; }
            public string expiry { get; set; }
            public string cardType { get; set; }
        }
        public class cardDetailsList
        {
            public CardDetails cardDetails { get; set; }
        }


        //3rd req
        public class Card
        {
            public string merchantSessionKey { get; set; }
            public string cardIdentifier { get; set; }
            public bool save { get; set; }
        }

        public class PaymentMethod
        {
            public Card card { get; set; }
        }

        public class BillingAddress
        {
            public string address1 { get; set; }
            public string city { get; set; }
            public string postalCode { get; set; }
            public string country { get; set; }
        }

        public class ShippingDetails
        {

            public string recipientFirstName { get; set; }
            public string recipientLastName { get; set; }
            public string shippingAddress1 { get; set; }
            public string shippingAddress2 { get; set; }
            public string shippingCity { get; set; }
            public string shippingState { get; set; }
            public string shippingPostalCode { get; set; }
            public string shippingCountry { get; set; }
        }

        public class OrderData
        {
            public string transactionType { get; set; }
            public PaymentMethod paymentMethod { get; set; }
            public string vendorTxCode { get; set; }
            public int amount { get; set; }
            public string currency { get; set; }
            public string description { get; set; }
            public string apply3DSecure { get; set; }
            public string customerFirstName { get; set; }
            public string customerLastName { get; set; }
            public BillingAddress billingAddress { get; set; }
            public ShippingDetails shippingDetails { get; set; }
            public string entryMethod { get; set; }
        }

        public class OrderResult
        {
            public string statusCode { get; set; }
            public string statusDetail { get; set; }
            public string transactionId { get; set; }
            public string acsUrl { get; set; }
            public string paReq { get; set; }
            public string status { get; set; }
        }
    }
}
public class Items
{
    public string productid { get; set; }
    public string quantity { get; set; }
    public Answer Answer { get; set; }
}
public class paymentData
{
    public string NameOnCard { get; set; }
    public string cardtype { get; set; }
    public string CardNumber { get; set; }
    public string CardCvc { get; set; }
    public string ExpireMonth { get; set; }
    public string ExpireYear { get; set; }
    public string PaymentAmount { get; set; }
    public string UserId { get; set; }
    public string ContactId { get; set; }
    public string city { get; set; }
    public string address1 { get; set; }
    public string firstname { get; set; }
    public string lastname { get; set; }
    public string email { get; set; }
    public string phone { get; set; }
    public string postalcode { get; set; }
    public string country { get; set; }
    public string unitSuit { get; set; }
    public string Currency { get; set; }
    public List<Items> item { get; set; }
    public BillingAddress billingaddress { get; set; }
    public ShippingDetails shippingDetails { get; set; }
    public string statusCode { get; set; }
    public string statusDetail { get; set; }
    public string transactionId { get; set; }
    public string acsUrl { get; set; }
    public string paReq { get; set; }
    public string status { get; set; }
    public string isMember { get; set; }
    public string deliveryCharge { get; set; }
    public List<ProdQuestionAnswers> prodQuestionAnswerList { get; set; }
}

public class ProdQuestionAnswers
{
    public string Answer { get; set; }
    public string QuestionId { get; set; }
}
//    // N TO N RELATIONSHIP ASSOCIATION
//    //EntityReferenceCollection relatedEntities = new EntityReferenceCollection();
//    //relatedEntities.Add(new EntityReference("my_customerorder", _customerorderId));
//    //Relationship relationship = new Relationship("my_my_customerorder_my_product_currencyandpr");
//    //_getorgService.Associate("my_product_currencyandprice", new Guid(productlistid[i].Id), relationship, relatedEntities);