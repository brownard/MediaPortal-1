use %TvLibrary%
GO
 
ALTER TABLE program
  ADD seriesId varchar(200)
GO

ALTER TABLE program
  ADD seriesTermination TINYINT
GO

ALTER TABLE schedule
  ADD seriesId varchar(200)
GO

