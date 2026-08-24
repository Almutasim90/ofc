namespace POS.Application.Channels;

public record SalesChannelDto(Guid Id, string NameAr, string NameEn, string? LogoUrl, bool IsActive, bool IsInStore);
public record UpsertSalesChannelRequest(string NameAr, string NameEn, string? LogoUrl, bool IsActive);
public record ProductChannelPriceDto(Guid ProductId, decimal? Price);
public record SetChannelPricesRequest(List<ProductChannelPriceDto> Prices);
