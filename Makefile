.PHONY: up down logs migrate seed ps clean build

up:
	docker compose up -d --build

down:
	docker compose down

logs:
	docker compose logs -f

migrate:
	dotnet ef database update --project src/NOC.Web --startup-project src/NOC.Web

seed:
	dotnet run --project src/NOC.Web -- --seed

ps:
	docker compose ps

clean:
	docker compose down -v

build:
	dotnet build NeurynOmnichannel.sln
