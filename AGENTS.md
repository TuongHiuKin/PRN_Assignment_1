# AIVES — Agent & Team Instructions

## Mục tiêu hiện hành

Ưu tiên **nhóm 6 — Phản hồi & báo cáo thi vấn đáp**: báo cáo cá nhân theo lượt thi, thống kê lớp và biểu đồ. Đây là định hướng mới người dùng đã xác nhận; News Management là module/lịch sử còn trong source, không phải tiêu chí nghiệm thu nhóm 6.

Nền kỹ thuật hiện hành vẫn là ASP.NET Core MVC .NET 8 + BLL + DAL, EF Core, LINQ, SQL Server. Không suy ra việc đổi nghiệp vụ cho phép đổi stack, tự đổi schema hoặc phục hồi project khác từ Git history.

## Đọc đúng tài liệu trước khi làm

- Architecture, feature, review: đọc `.agents/skills/funews-architecture/SKILL.md`. Tên skill được giữ để tương thích các lời gọi cũ, nội dung đã theo nhóm 6.
- Git tasks: đọc `.agents/skills/team-git-workflow/SKILL.md`.
- Nghiệp vụ đã duyệt: `docs/business-rules.md` (DEC-01..DEC-12).
- Kiến trúc/trạng thái: `docs/ARCHITECTURE.md`.
- Task/phụ thuộc: `docs/TASKS.md`; nghiệm thu: `docs/CODE-REVIEW-CHECKLIST.md`.
- Phạm vi triển khai, contract và fixture: `docs/G6-IMPLEMENTATION-HANDOFF.md`. Phân biệt quyết định đã duyệt với phương án kỹ thuật/fixture đề xuất.
- `docs/archive/` chỉ lưu lịch sử News; không dùng làm hướng dẫn nghiệp vụ nhóm 6.

## Ranh giới và an toàn

- Kiến trúc 3 lớp: `StudentNameMVC` (Presentation) → `AIVES.BLL` (Business Logic) → `AIVES.DAL` (Data Access) → SQL Server. `StudentNameMVC` là project Presentation vật lý đại diện cho tầng MVC của AIVES.
- Luồng dữ liệu chuẩn: View → Controller → Service → Repository → AIVESDbContext → SQL Server. Controller không gọi trực tiếp DbContext/Repository; BLL không dùng DbContext.
- MVC → BLL → DAL; MVC cấu hình DAL tại Program.cs. Không reference vòng. Repositories và AIVESDbContext luôn Scoped; không biến Repository hay DbContext thành Singleton. Repository chứa toàn bộ logic EF Core/LINQ và persistence.
- Không tự map Staff thành Sinh viên hoặc đổi mã role News; kiểm tra principal và phạm vi dữ liệu theo contract được duyệt.
- Chỉ trong workspace hiện tại. Bảo toàn module cũ và thay đổi teammate; không reset/clean/discard để thuận tiện.
- Review chỉ đọc/test; implement cần yêu cầu riêng nêu task/phạm vi. Không coi tài liệu này là quyền tự triển khai tất cả.
- Không tự chạy script DB, migration, seed, test ghi DB hoặc đổi framework/package. Thay đổi schema/Auth/file chung phải có owner và phạm vi cho phép.
- Mock chỉ ở test/harness được nhận diện; không bypass production authentication hoặc tuyên bố mock là AI/SQL thật.
- Build/test phần sửa trước bàn giao; công khai phần chưa kiểm chứng và snapshot review.

## Git

Nhánh task theo `<member-name>/<feature-name>`; xác định người làm trước khi tạo nhánh. Kiểm tra status và bảo toàn thay đổi sẵn có; chỉ stage file đúng task.
Không tự commit/push/PR/merge nếu chưa có yêu cầu rõ. Không force-push hoặc push thẳng main. Unit tests phải pass trước PR; không merge main không qua review.
