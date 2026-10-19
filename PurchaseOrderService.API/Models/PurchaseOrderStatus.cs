using System.ComponentModel.DataAnnotations;

namespace PurchaseOrderService.API.Models;

public enum PurchaseOrderStatus { Draft = 0, Issued = 1, Cancelled = 2 }
