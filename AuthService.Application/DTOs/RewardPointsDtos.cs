namespace AuthService.Application.DTOs;

public class RewardPointsDto
{
    public int Balance { get; set; }
    public List<RewardPointsLogDto> History { get; set; } = new();
}

public class RewardPointsLogDto
{
    public int Points { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ReferenceId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class EarnPointsDto
{
    public int UserId { get; set; }
    public decimal AmountPaid { get; set; }
    public string? ReferenceId { get; set; } // PNR or BookingId
}

public class RedeemPointsDto
{
    public int PointsToRedeem { get; set; }
    public decimal BookingTotal { get; set; }  // Used to enforce 60% max cap
    public string? ReferenceId { get; set; }
}

public class RedeemPointsResultDto
{
    public int PointsUsed { get; set; }
    public decimal DiscountAmount { get; set; }
    public int RemainingBalance { get; set; }
}
