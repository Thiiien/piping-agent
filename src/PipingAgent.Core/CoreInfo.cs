namespace PipingAgent.Core
{
    /// <summary>
    /// Thông tin chung của lõi xử lý. Lõi không tham chiếu thư viện CAD, giao diện hay mạng.
    /// Xem mục 6.12.3 của docs/TAI_LIEU_DU_AN.md.
    /// </summary>
    public static class CoreInfo
    {
        /// <summary>Phiên bản lược đồ của mô hình trung gian Piping IR.</summary>
        public const string IrSchemaVersion = "1.0";

        /// <summary>Phiên bản lược đồ của hồ sơ quy ước.</summary>
        public const string ProfileSchemaVersion = "1.0";

        /// <summary>Phiên bản lược đồ của danh mục ống và phụ kiện.</summary>
        public const string CatalogSchemaVersion = "1.0";

        /// <summary>Đơn vị chiều dài duy nhất bên trong lõi.</summary>
        public const string Units = "mm";
    }
}
