# 포켓몬 도감 DB

`Pokemon.db`는 SQLite 데이터베이스이며, `Pokemon` 테이블에 전국도감 1~649번을 각 1행씩 저장합니다. 스키마는 `schema.sql`에 있습니다.

`Type` 테이블은 5세대의 17개 타입 ID(`type_id`), 한국어 이름(`name`), 터미널 출력용 RGB 색상(`color_rgb`, `#RRGGBB`)을 저장합니다. `Pokemon.type1_id`, `Pokemon.type2_id`, `PokemonMove.type_id`는 모두 `Type.type_id`를 참조합니다. 단일 타입 포켓몬의 `type2_id`만 `NULL`입니다.

`TypeEffectiveness` 테이블은 공격 타입 ID(`attack_type_id`), 방어 타입 ID(`defend_type_id`), 배율(`multiplier`)을 저장합니다. 공격·방어 타입 ID의 조합이 기본 키이며, 두 ID 모두 `Type`을 참조합니다. 17개 타입의 모든 조합 289개를 저장하므로 1배 상성도 행으로 존재합니다. 5세대까지의 상성을 적용해 고스트·악 기술은 강철에 0.5배입니다.

`PokemonMove` 테이블은 기술 순번(`move_id`)을 기본 키로 사용하며, 이름(`name`), 타입 ID(`type_id`), 분류(`category`), 위력(`power`), 명중률(`accuracy`), PP(`pp`)를 저장합니다. 위력과 명중률은 값이 없는 기술을 위해 `NULL`을 허용하고, 나머지 열은 `NOT NULL`입니다.

- 기술 1~559번을 각 1행씩 저장합니다. 이름과 타입은 한국어이며, 분류는 `물리`·`특수`·`변화`입니다.
- 위력이나 명중률이 원본에 없으면 `NULL`입니다. 원본의 `0`은 별도 값으로 그대로 저장합니다.
- 560번 이후 기술은 수록하지 않습니다.

- `pokemon_id`: 전국도감 번호. 폼별 내부 ID가 아닌 종의 `species_id`를 사용합니다.
- `name`: 한국어 이름. `type1_id`, `type2_id`의 순서는 원본의 타입 슬롯 1, 2를 따릅니다.
- 6개 능력치는 레벨별 실능력치가 아닌 종족값입니다.
- 각 종은 PokéAPI의 기본 폼(`is_default=1`)을 사용합니다. 리전 폼, 메가진화, 기타 대체 폼은 별도 행으로 넣지 않습니다.
- 650번 이후 포켓몬은 수록하지 않습니다. 페어리로 변경된 포켓몬 22종과 기술 3개의 타입은 원본의 과거 변경 기록을 사용해 5세대 값으로 복원했습니다. 종족값과 기술의 위력·명중률·PP는 원본 버전의 현재 값이므로 5세대 당시 값과 다를 수 있습니다.

## 출처와 재현

출처: [PokéAPI CSV 데이터](https://github.com/PokeAPI/pokeapi/tree/bc92d3b6029ef1abe9e7ad424c400b338f3c11fe/data/v2/csv), [PokéAPI 문서](https://pokeapi.co/docs/v2).

- 다운로드 날짜: 2026-10-01
- 원본 커밋: `bc92d3b6029ef1abe9e7ad424c400b338f3c11fe`
- `source/`에 포켓몬과 기술 생성에 사용한 CSV 원본을 보관했습니다. 재생성 시 네트워크와 외부 Python 패키지가 필요하지 않습니다.
- 프로젝트 루트에서 `python Pokemon_server/DB/build_db.py`를 실행합니다. 기존 DB가 있으면 덮어쓰지 않으므로, 재생성하려면 먼저 기존 파일을 다른 이름으로 보관해야 합니다.

생성 과정에서 포켓몬 1~649번과 기술 1~559번의 연속성·중복·누락, 필수 데이터, 종족값 범위, 타입 외래 키, 289개 상성, 저장 결과와 원본 일치, SQLite 무결성을 검사합니다.

## 조회 예시

```sql
SELECT * FROM Pokemon WHERE pokemon_id = 25;
SELECT COUNT(*), MIN(pokemon_id), MAX(pokemon_id) FROM Pokemon;
SELECT * FROM PokemonMove WHERE move_id = 1;
SELECT COUNT(*), MIN(move_id), MAX(move_id) FROM PokemonMove;
SELECT t1.name AS type1, t2.name AS type2
FROM Pokemon AS p
JOIN Type AS t1 ON t1.type_id = p.type1_id
LEFT JOIN Type AS t2 ON t2.type_id = p.type2_id
WHERE p.pokemon_id = 25;
SELECT e.multiplier
FROM TypeEffectiveness AS e
JOIN Type AS attack ON attack.type_id = e.attack_type_id
JOIN Type AS defend ON defend.type_id = e.defend_type_id
WHERE attack.name = '불꽃' AND defend.name = '풀';
SELECT type_id, name, color_rgb FROM Type ORDER BY type_id;
```

서버에서 사용할 연결 문자열 예: `Data Source=DB/Pokemon.db;Mode=ReadOnly`.
상대 경로는 실행 시 작업 디렉터리 기준이므로, 서버 실행 위치에 맞춰 절대 경로를 지정하거나 DB 경로를 구성해야 합니다.
현재 서버는 시작 시 포켓몬·기술·타입·상성 데이터를 읽기 전용 메모리 사전에 적재하고, 클라이언트 요청 처리에는 적재한 데이터를 사용합니다. DB 내용을 바꾼 뒤에는 서버를 다시 시작해야 합니다.
