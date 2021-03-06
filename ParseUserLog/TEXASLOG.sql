USE [TEXASLOG];
GO

IF OBJECT_ID( '[DIGESTED]' ) IS NOT NULL DROP TABLE [DIGESTED];
GO

CREATE TABLE [DIGESTED] (
    [TIME]   DATETIME    NOT NULL CONSTRAINT [PK_DIGESTED] PRIMARY KEY
   ,[CARDS]  VARCHAR(8)  NOT NULL
   ,[BOARD]  VARCHAR(16) NOT NULL
   ,[ACTION] VARCHAR(8)  NOT NULL
   ,[RANK]   REAL        NOT NULL
);
CREATE NONCLUSTERED INDEX [IX_DIGESTED_CARDS_BOARD_ACTION] ON [DIGESTED]( [CARDS] ASC, [BOARD] ASC, [ACTION] ASC );
GO
