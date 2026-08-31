namespace POS.Application.Channels;

public record SalesChannelDto(Guid Id, string Code, string NameAr, string NameEn, string? LogoUrl, bool IsActive, bool IsInStore);
public record UpsertSalesChannelRequest(string Code, string NameAr, string NameEn, string? LogoUrl, bool IsActive);
public record BranchChannelAvailabilityDto(Guid BranchId,Guid SalesChannelId,bool IsEnabled,bool RequiresPrepayment);
public record SetBranchChannelAvailabilityRequest(bool IsEnabled,bool RequiresPrepayment);
public record ProductChannelPriceDto(Guid ProductId, decimal? Price);
public record SetChannelPricesRequest(List<ProductChannelPriceDto> Prices);
