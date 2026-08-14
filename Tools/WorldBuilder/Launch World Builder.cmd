@echo off
setlocal
where py >nul 2>nul
if errorlevel 1 goto python_fallback
py -3 "%~dp0world_builder.py" %*
exit /b %errorlevel%

:python_fallback
python "%~dp0world_builder.py" %*
exit /b %errorlevel%
