namespace Common.Auth
{
    /// <summary>
    /// Authorization policy name constants for all microservices
    /// </summary>
    public static class PolicyConstants
    {
        // Catalog Service Policies
        public const string ReadCatalog = "ReadCatalog";
        public const string WriteCatalog = "WriteCatalog";
        
        // Basket Service Policies
        public const string ReadBasket = "ReadBasket";
        public const string WriteBasket = "WriteBasket";
        
        // Discount Service Policies
        public const string ReadDiscount = "ReadDiscount";
        public const string WriteDiscount = "WriteDiscount";
        
        // Ordering Service Policies
        public const string ReadOrders = "ReadOrders";
        public const string WriteOrders = "WriteOrders";
        
        // Shopping Aggregator Policies
        public const string ReadShopping = "ReadShopping";
        
        // Admin Policies (across all services)
        public const string AdminAccess = "AdminAccess";
        
        // Scope Constants (OAuth2 scopes)
        public static class Scopes
        {
            public const string CatalogRead = "catalog.read";
            public const string CatalogWrite = "catalog.write";
            public const string BasketRead = "basket.read";
            public const string BasketWrite = "basket.write";
            public const string DiscountRead = "discount.read";
            public const string DiscountWrite = "discount.write";
            public const string OrdersRead = "orders.read";
            public const string OrdersWrite = "orders.write";
            public const string ShoppingRead = "shopping.read";
        }
        
        // Role Constants
        public static class Roles
        {
            public const string Admin = "Admin";
            public const string SuperAdmin = "SuperAdmin";
            public const string User = "User";
            public const string CatalogReader = "CatalogReader";
            public const string CatalogAdmin = "CatalogAdmin";
            public const string OrderManager = "OrderManager";
        }
    }
}

