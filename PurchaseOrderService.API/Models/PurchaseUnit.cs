using System.ComponentModel.DataAnnotations;

namespace PurchaseOrderService.API.Models;

public enum PurchaseUnit { Hour = 0, Day = 1, Month = 2, FixedFee = 3, Piece = 4 }
