# GitHub Copilot Instructions — Clean Architecture

> These instructions guide GitHub Copilot to generate code aligned with Clean Architecture principles and senior-level engineering standards. Place this file at `.github/copilot-instructions.md` in the root of your repository.

---

## 🏛️ Architecture Overview

This solution follows **Clean Architecture** (Robert C. Martin). Enforce strict layer separation at all times:
Put each layer on a diferent visual studio project and enforce the dependency rule with project references. The layers are:
```
src/
├── domain/           # Enterprise business rules (innermost layer)
├── application/      # Application business rules (use cases)
├── infrastructure/   # Frameworks, DB, external services (outermost layer)
└── presentation/     # UI / API controllers / CLI
```

**Dependency Rule (non-negotiable):**
- Dependencies always point **inward**.
- `domain` depends on nothing.
- `application` depends only on `domain`.
- `infrastructure` and `presentation` depend on `application` and `domain`.
- **Never** import from `infrastructure` or `presentation` inside `domain` or `application`.

---

## 📦 Layer Responsibilities

### `domain/`
- Contains **Entities**, **Value Objects**, **Domain Events**, **Aggregates**, and **Domain Services**.
- Pure business logic — no framework code, no I/O, no dependencies.
- All domain objects must be immutable where possible.
- Entities own their invariants and throw domain-specific errors when violated.

```ts
// ✅ GOOD — domain entity with invariant enforcement
export class Email {
  private constructor(private readonly value: string) {}

  static create(raw: string): Email {
    if (!raw.includes('@')) throw new InvalidEmailError(raw);
    return new Email(raw.toLowerCase().trim());
  }

  toString(): string { return this.value; }
}
```

### `application/`
- Contains **Use Cases** (one class per use case), **Repository interfaces**, **Service interfaces**, **DTOs**, and **Application errors**.
- Orchestrates domain objects — no business logic lives here.
- Each use case has a single `execute(dto)` method.
- Depends on **interfaces**, never concrete implementations.

```ts
// ✅ GOOD — use case depending on abstraction
export class CreateUserUseCase {
  constructor(
    private readonly userRepo: IUserRepository,
    private readonly hasher: IPasswordHasher,
    private readonly eventBus: IEventBus,
  ) {}

  async execute(dto: CreateUserDTO): Promise<UserResponseDTO> {
    const email = Email.create(dto.email);
    const hash = await this.hasher.hash(dto.password);
    const user = User.create({ email, passwordHash: hash });
    await this.userRepo.save(user);
    await this.eventBus.publish(new UserCreatedEvent(user.id));
    return UserMapper.toDTO(user);
  }
}
```

### `infrastructure/`
- Implements all interfaces defined in `application/`.
- Contains ORM models, external API clients, email providers, queue adapters, etc.
- Framework-specific code is **only** here.
- Repository implementations map between ORM/persistence models and domain entities.

### `presentation/`
- REST controllers, GraphQL resolvers, CLI commands, WebSocket handlers.
- Validates and transforms raw input into **Commands**, **Queries**, or DTOs.
- Dispatches through the **Mediator** — never calls use cases directly.
- Returns HTTP responses; maps application errors to HTTP status codes.

---

## ⚡ CQRS — Command Query Responsibility Segregation

Separate every operation into either a **Command** (mutates state, returns void or an ID) or a **Query** (reads state, never mutates). This lives entirely in the `application/` layer.

### Core Contracts

```ts
// application/cqrs/command.interface.ts
export interface ICommand {}

// application/cqrs/command-handler.interface.ts
export interface ICommandHandler<TCommand extends ICommand, TResult = void> {
  handle(command: TCommand): Promise<TResult>;
}

// application/cqrs/query.interface.ts
export interface IQuery<TResult> {}

// application/cqrs/query-handler.interface.ts
export interface IQueryHandler<TQuery extends IQuery<TResult>, TResult> {
  handle(query: TQuery): Promise<TResult>;
}
```

### Command Example

Commands represent **intent to change state**. Name them as imperatives.

```ts
// application/commands/create-user/create-user.command.ts
export class CreateUserCommand implements ICommand {
  constructor(
    public readonly email: string,
    public readonly password: string,
  ) {}
}

// application/commands/create-user/create-user.handler.ts
@injectable()
export class CreateUserHandler implements ICommandHandler<CreateUserCommand, string> {
  constructor(
    private readonly userRepo: IUserRepository,
    private readonly hasher: IPasswordHasher,
    private readonly eventBus: IEventBus,
  ) {}

  async handle(command: CreateUserCommand): Promise<string> {
    const email = Email.create(command.email);
    const hash = await this.hasher.hash(command.password);
    const user = User.create({ email, passwordHash: hash });
    await this.userRepo.save(user);
    await this.eventBus.publish(new UserCreatedEvent(user.id));
    return user.id.toString();
  }
}
```

### Query Example

Queries are **read-only**. They may use optimized read models (e.g., raw SQL, views, projections) — they don't need to go through domain entities.

```ts
// application/queries/get-user/get-user.query.ts
export class GetUserQuery implements IQuery<UserResponseDTO> {
  constructor(public readonly userId: string) {}
}

// application/queries/get-user/get-user.handler.ts
@injectable()
export class GetUserHandler implements IQueryHandler<GetUserQuery, UserResponseDTO> {
  constructor(private readonly readRepo: IUserReadRepository) {}

  async handle(query: GetUserQuery): Promise<UserResponseDTO> {
    const user = await this.readRepo.findById(query.userId);
    if (!user) throw new UserNotFoundError(query.userId);
    return user;
  }
}
```

### CQRS Rules
- ✅ Commands may return a created resource ID, but never a full entity/DTO.
- ✅ Queries never trigger writes — not even audit logs inside the handler.
- ✅ Read models in queries can bypass domain entities for performance.
- ❌ Never mix command and query logic in the same handler.
- ❌ Never call a command handler from inside a query handler or vice versa.

---

## 🎯 Mediator Pattern

The **Mediator** decouples the sender (controller) from the receiver (handler). Controllers dispatch commands and queries through the mediator — they never reference handlers directly.

### Mediator Interface

```ts
// application/mediator/mediator.interface.ts
export interface IMediator {
  send<TResult>(request: ICommand | IQuery<TResult>): Promise<TResult>;
}
```

### In-process Mediator Implementation

```ts
// infrastructure/mediator/mediator.ts
@injectable()
export class Mediator implements IMediator {
  private readonly handlers = new Map<string, ICommandHandler<any, any> | IQueryHandler<any, any>>();

  register<T>(requestClass: new (...args: any[]) => T, handler: ICommandHandler<any> | IQueryHandler<any, any>): void {
    this.handlers.set(requestClass.name, handler);
  }

  async send<TResult>(request: object): Promise<TResult> {
    const key = request.constructor.name;
    const handler = this.handlers.get(key);
    if (!handler) throw new Error(`No handler registered for "${key}"`);
    return handler.handle(request as any);
  }
}
```

> **Alternative**: Use a battle-tested library like [`@nestjs/cqrs`](https://docs.nestjs.com/recipes/cqrs) or [`mediatr-ts`](https://github.com/Eugeny/mediatr-ts) instead of rolling your own.

### Registration in Composition Root

```ts
// infrastructure/di/container.ts
const mediator = container.resolve(Mediator);

mediator.register(CreateUserCommand, container.resolve(CreateUserHandler));
mediator.register(GetUserQuery,     container.resolve(GetUserHandler));
```

### Controller Using Mediator

```ts
// presentation/http/controllers/user.controller.ts
@injectable()
export class UserController {
  constructor(private readonly mediator: IMediator) {}

  async create(req: Request, res: Response): Promise<void> {
    const command = new CreateUserCommand(req.body.email, req.body.password);
    const userId = await this.mediator.send<string>(command);
    res.status(201).json({ id: userId });
  }

  async getById(req: Request, res: Response): Promise<void> {
    const query = new GetUserQuery(req.params.id);
    const user = await this.mediator.send<UserResponseDTO>(query);
    res.status(200).json(user);
  }
}
```

### Mediator Pipeline Behaviors (Cross-cutting Concerns)

Use **pipeline behaviors** (middleware chain) to handle logging, validation, and transactions without polluting handlers.

```ts
// application/mediator/pipeline-behavior.interface.ts
export interface IPipelineBehavior<TRequest, TResponse> {
  handle(request: TRequest, next: () => Promise<TResponse>): Promise<TResponse>;
}

// application/behaviors/logging.behavior.ts
export class LoggingBehavior<TRequest, TResponse>
  implements IPipelineBehavior<TRequest, TResponse> {

  constructor(private readonly logger: ILogger) {}

  async handle(request: TRequest, next: () => Promise<TResponse>): Promise<TResponse> {
    const name = (request as any).constructor.name;
    this.logger.info(`[Mediator] Handling ${name}`);
    const result = await next();
    this.logger.info(`[Mediator] ${name} completed`);
    return result;
  }
}

// application/behaviors/validation.behavior.ts
export class ValidationBehavior<TRequest extends object, TResponse>
  implements IPipelineBehavior<TRequest, TResponse> {

  constructor(private readonly validator: IValidator<TRequest>) {}

  async handle(request: TRequest, next: () => Promise<TResponse>): Promise<TResponse> {
    const errors = await this.validator.validate(request);
    if (errors.length > 0) throw new ValidationError(errors);
    return next();
  }
}
```

**Register behaviors** in order: Logging → Validation → Transaction → Handler.

---

## ✅ General Coding Standards

### Naming
- Use **intention-revealing names**. No abbreviations (`usr`, `mgr`, `tmp`).
- Classes: `PascalCase`. Functions/variables: `camelCase`. Constants: `SCREAMING_SNAKE_CASE`. Files: `kebab-case.ts`.
- Use case files: `create-user.use-case.ts`. Repository interface: `user.repository.interface.ts`.

### Functions & Methods
- Functions do **one thing** (Single Responsibility).
- Max **3 parameters** per function. Wrap more in an object/DTO.
- Prefer **pure functions** in domain and application layers.
- **No side effects** in domain layer.
- Early return pattern over nested conditionals.

```ts
// ✅ GOOD — early return, single responsibility
async function findActiveUser(id: string): Promise<User> {
  const user = await userRepo.findById(id);
  if (!user) throw new UserNotFoundError(id);
  if (!user.isActive()) throw new InactiveUserError(id);
  return user;
}
```

### Error Handling
- Define domain errors in `domain/errors/` extending a base `DomainError`.
- Define application errors in `application/errors/` extending `ApplicationError`.
- **Never swallow errors** with empty catch blocks.
- Map errors to HTTP responses only in the `presentation/` layer.
- Use `Result<T, E>` pattern (or a library like `neverthrow`) over throwing in use cases when the error is an expected outcome.

```ts
// ✅ GOOD — typed error hierarchy
export class DomainError extends Error {
  constructor(message: string, public readonly code: string) {
    super(message);
    this.name = this.constructor.name;
  }
}

export class InvalidEmailError extends DomainError {
  constructor(email: string) {
    super(`"${email}" is not a valid email address.`, 'INVALID_EMAIL');
  }
}
```

### Interfaces & Abstractions
- Define interfaces for every external dependency (DB, mailer, queue, clock, etc.).
- Use `I` prefix for interfaces: `IUserRepository`, `IEventBus`.
- Program to interfaces in `application/` — never to concrete classes.

### Dependency Injection
- Use a DI container (e.g., `tsyringe`, `InversifyJS`, NestJS modules, or manual wiring in `main.ts`).
- Register bindings in the composition root (`infrastructure/di/` or `main.ts`).
- Constructors accept only interfaces, never instantiate dependencies internally.

---

## 🔒 SOLID Principles

| Principle | Enforcement |
|-----------|------------|
| **S** — Single Responsibility | One reason to change per class/function |
| **O** — Open/Closed | Extend behavior via new classes, not modifying existing ones |
| **L** — Liskov Substitution | Subtypes must honor contracts of base types |
| **I** — Interface Segregation | Small, focused interfaces over fat ones |
| **D** — Dependency Inversion | Depend on abstractions, not concretions |

---

## 🧪 Testing Standards

- **Unit tests** for domain entities, value objects, and command/query handlers (mock all dependencies).
- **Integration tests** for repositories and infrastructure adapters.
- **E2E tests** for API endpoints (presentation layer).
- Test file naming: `*.spec.ts` (unit), `*.integration.spec.ts`, `*.e2e.spec.ts`.
- Use **AAA pattern**: Arrange → Act → Assert.
- Minimum coverage targets: domain 100%, application 90%, infrastructure 70%.
- No logic in test setup helpers — keep tests readable and self-contained.
- Test each command handler and query handler in isolation — never test through the mediator in unit tests.

```ts
// ✅ GOOD — command handler unit test
describe('CreateUserHandler', () => {
  it('should publish UserCreatedEvent after saving user', async () => {
    const { sut, eventBus } = makeSut();
    const command = new CreateUserCommand('jane@example.com', 'secret123');

    await sut.handle(command);

    expect(eventBus.publish).toHaveBeenCalledWith(expect.any(UserCreatedEvent));
  });
});
```

---

## 🚫 Anti-patterns to Avoid

- ❌ **Anemic domain models** — entities with only getters/setters and no behavior.
- ❌ **Fat controllers** — business logic in HTTP handlers.
- ❌ **God classes** — classes doing too many things.
- ❌ **Leaky abstractions** — ORM models or DB types leaking into the domain.
- ❌ **Magic strings/numbers** — always use named constants or enums.
- ❌ **`any` type** (TypeScript) — use proper types or `unknown` with type guards.
- ❌ **Circular dependencies** — restructure code if a cycle appears.
- ❌ **Direct `new` in application/domain** for infrastructure services — always inject.
- ❌ **Queries that mutate state** — queries must be 100% side-effect-free.
- ❌ **Commands returning full entities/DTOs** — return only an ID or void.
- ❌ **Controllers importing handlers directly** — always go through `IMediator`.
- ❌ **Business logic in pipeline behaviors** — behaviors handle cross-cutting concerns only (logging, validation, transactions).

---

## 📁 File Structure Example

```
src/
├── domain/
│   ├── entities/
│   │   └── user.entity.ts
│   ├── value-objects/
│   │   ├── email.value-object.ts
│   │   └── user-id.value-object.ts
│   ├── events/
│   │   └── user-created.event.ts
│   └── errors/
│       └── invalid-email.error.ts
│
├── application/
│   ├── commands/
│   │   └── create-user/
│   │       ├── create-user.command.ts
│   │       ├── create-user.handler.ts
│   │       └── create-user.handler.spec.ts
│   ├── queries/
│   │   └── get-user/
│   │       ├── get-user.query.ts
│   │       ├── get-user.handler.ts
│   │       └── get-user.handler.spec.ts
│   ├── behaviors/
│   │   ├── logging.behavior.ts
│   │   ├── validation.behavior.ts
│   │   └── transaction.behavior.ts
│   ├── cqrs/
│   │   ├── command.interface.ts
│   │   ├── command-handler.interface.ts
│   │   ├── query.interface.ts
│   │   └── query-handler.interface.ts
│   ├── mediator/
│   │   ├── mediator.interface.ts
│   │   └── pipeline-behavior.interface.ts
│   ├── interfaces/
│   │   ├── user.repository.interface.ts
│   │   ├── password-hasher.interface.ts
│   │   └── event-bus.interface.ts
│   └── errors/
│       └── user-not-found.error.ts
│
├── infrastructure/
│   ├── database/
│   │   ├── prisma/
│   │   │   └── user.prisma.repository.ts
│   │   └── mappers/
│   │       └── user.mapper.ts
│   ├── services/
│   │   └── bcrypt-password-hasher.ts
│   ├── mediator/
│   │   └── mediator.ts
│   └── di/
│       └── container.ts
│
└── presentation/
    ├── http/
    │   ├── controllers/
    │   │   └── user.controller.ts
    │   ├── middlewares/
    │   └── mappers/
    │       └── user-response.mapper.ts
    └── validators/
        └── create-user.validator.ts
```

---

## 🔁 Mapper Pattern

Always use **mappers** to translate between layers. Never expose persistence models to the domain or application layers.

```ts
// infrastructure/database/mappers/user.mapper.ts
export class UserMapper {
  static toDomain(raw: UserPrismaModel): User {
    return User.reconstitute({
      id: UserId.from(raw.id),
      email: Email.create(raw.email),
      passwordHash: raw.passwordHash,
      createdAt: raw.createdAt,
    });
  }

  static toPersistence(user: User): Prisma.UserCreateInput {
    return {
      id: user.id.toString(),
      email: user.email.toString(),
      passwordHash: user.passwordHash,
      createdAt: user.createdAt,
    };
  }

  static toDTO(user: User): UserResponseDTO {
    return { id: user.id.toString(), email: user.email.toString() };
  }
}
```

---

## 📝 Code Comments Policy

- **No comments for obvious code** — code should be self-explanatory.
- Use JSDoc for **public API surfaces** (use case `execute`, entity factories).
- Use comments only to explain **"why"**, never **"what"**.
- Mark technical debt with `// TODO(author): description` — never leave silent debt.

---

## 🔐 Security Defaults

- Never log passwords, tokens, or PII.
- Always validate and sanitize input at the `presentation/` boundary.
- Use parameterized queries — never string-concatenate SQL.
- Secrets come from environment variables only — never hardcoded.
- Apply the principle of least privilege for all external service credentials.

---

## 🚀 Performance Defaults

- Avoid N+1 queries — use eager loading or dataloaders for relational data.
- Paginate all collection endpoints.
- Make use cases stateless and side-effect-free where possible to enable caching.
- Prefer async/await over nested callbacks.

---

*These instructions are enforced across all code suggestions. When in doubt, favor explicitness, simplicity, and adherence to the dependency rule over brevity.*
