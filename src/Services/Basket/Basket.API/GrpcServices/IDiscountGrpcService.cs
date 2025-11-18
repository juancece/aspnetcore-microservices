using Discount.Grpc.Protos;

namespace Basket.API.GrpcServices
{
    public interface IDiscountGrpcService
    {
        Task<CouponModel> GetDiscount(string productName);
    }
}

