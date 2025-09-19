--[0][0] 직원: Employees 
CREATE TABLE [dbo].[Employees]
(
    [Id]        BIGINT             IDENTITY (1, 1) NOT NULL PRIMARY KEY,    -- 직원 고유 아이디, 자동 증가
    [Active]    BIT                DEFAULT ((1)) NULL,                      -- 활성 상태 표시, 기본값 1 (활성)
    [CreatedAt] DATETIMEOFFSET NULL DEFAULT SYSDATETIMEOFFSET(),            -- 레코드 생성 시간
    [CreatedBy] NVARCHAR (255)     NULL,                                    -- 레코드 생성자 이름
    [Name]      NVARCHAR (MAX)     NULL,                                    -- 이름
    ---
    [FirstName] NVARCHAR (255)     NULL,                                    -- 이름 (First Name)  
    [LastName]  NVARCHAR (255)     NULL,                                    -- 성 (Last Name
);
