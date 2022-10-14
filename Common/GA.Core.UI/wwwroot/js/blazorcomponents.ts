class Component1 {
    sayHi(message: string) {
        console.log(`Component1 says ${message}`);
    }
}

export function GetInstance() {
    return new Component1();
}

export function hello(): void {
    console.log('Hello');
}